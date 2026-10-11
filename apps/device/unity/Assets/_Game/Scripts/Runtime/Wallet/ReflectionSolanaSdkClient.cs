using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Plemu.Wallet
{
    /// <summary>
    /// Dynamic runtime binding to the official Solana.Unity.SDK package (Magicblock / Solana Mobile Stack).
    /// Uses safe reflection to interface with Web3.Instance and SolanaWalletAdapter without creating hard
    /// compile-time assembly couplings for standalone CLI test harnesses.
    /// </summary>
    public class ReflectionSolanaSdkClient : ISolanaSdkClient
    {
        private readonly Type _web3Type;
        private readonly Type _solanaWalletAdapterType;
        private readonly bool _isAvailable;

        public bool IsAvailable => _isAvailable;

        public ReflectionSolanaSdkClient()
        {
            _web3Type = FindType("Solana.Unity.SDK.Web3");
            _solanaWalletAdapterType = FindType("Solana.Unity.SDK.SolanaWalletAdapter");
            _isAvailable = _web3Type != null || _solanaWalletAdapterType != null;
        }

        public async Task<SolanaSdkAccountResult> LoginWalletAdapterAsync(
            string cluster,
            string identityName,
            string identityUri,
            string identityIcon,
            CancellationToken cancellationToken)
        {
            if (!_isAvailable)
            {
                return SolanaSdkAccountResult.Failed("Solana.Unity.SDK is not loaded in the current runtime.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return SolanaSdkAccountResult.Cancelled("User cancelled login request.");
            }

            try
            {
                object web3Instance = GetWeb3Instance();
                if (web3Instance != null)
                {
                    MethodInfo loginMethod = _web3Type.GetMethod("LoginWalletAdapter", Type.EmptyTypes);
                    if (loginMethod != null)
                    {
                        var taskObj = loginMethod.Invoke(web3Instance, null);
                        if (taskObj is Task task)
                        {
                            await task.ConfigureAwait(false);
                            PropertyInfo resultProp = task.GetType().GetProperty("Result");
                            object accountObj = resultProp?.GetValue(task);
                            if (accountObj == null)
                            {
                                return SolanaSdkAccountResult.Cancelled("User cancelled login in wallet adapter.");
                            }

                            string pubKey = ExtractPublicKey(accountObj);
                            string authToken = ExtractAuthToken(web3Instance) ?? "mwa-session-" + Guid.NewGuid().ToString("N");
                            return SolanaSdkAccountResult.Success(pubKey, authToken);
                        }
                    }
                }

                return SolanaSdkAccountResult.Failed("Failed to invoke Solana SDK LoginWalletAdapter via Web3.");
            }
            catch (Exception ex)
            {
                return SolanaSdkAccountResult.Failed($"Solana SDK login error: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        public async Task<SolanaSdkAccountResult> ReauthorizeWalletAdapterAsync(
            string authToken,
            string identityName,
            string identityUri,
            string identityIcon,
            CancellationToken cancellationToken)
        {
            if (!_isAvailable)
            {
                return SolanaSdkAccountResult.Failed("Solana.Unity.SDK is not loaded in the current runtime.");
            }

            // In MWA 2.0 through Solana.Unity.SDK, LoginWalletAdapter reuses the cached session / auth token
            return await LoginWalletAdapterAsync("devnet", identityName, identityUri, identityIcon, cancellationToken);
        }

        public Task DisconnectWalletAdapterAsync(string authToken, CancellationToken cancellationToken)
        {
            if (!_isAvailable)
            {
                return Task.CompletedTask;
            }

            try
            {
                object web3Instance = GetWeb3Instance();
                if (web3Instance != null)
                {
                    MethodInfo logoutMethod = _web3Type.GetMethod("Logout", Type.EmptyTypes) ??
                                              _web3Type.GetMethod("DisconnectWalletAdapter", Type.EmptyTypes);
                    logoutMethod?.Invoke(web3Instance, null);
                }
            }
            catch (Exception)
            {
                // Fail-safe cleanup
            }

            return Task.CompletedTask;
        }

        public async Task<SolanaSdkTransactionResult> SignAndSendTransactionAsync(
            string authToken,
            byte[] serializedTransaction,
            string cluster,
            CancellationToken cancellationToken)
        {
            if (!_isAvailable)
            {
                return SolanaSdkTransactionResult.Failed("Solana.Unity.SDK is not loaded in the current runtime.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return SolanaSdkTransactionResult.Cancelled("Transaction signing was cancelled.");
            }

            try
            {
                object web3Instance = GetWeb3Instance();
                if (web3Instance != null)
                {
                    PropertyInfo walletProp = _web3Type.GetProperty("Wallet");
                    object walletObj = walletProp?.GetValue(web3Instance);
                    if (walletObj != null)
                    {
                        MethodInfo signMethod = walletObj.GetType().GetMethods()
                            .FirstOrDefault(m => m.Name == "SignAndSendTransaction");

                        if (signMethod != null)
                        {
                            object param = DeserializeTransactionIfRequired(signMethod.GetParameters(), serializedTransaction);
                            object[] invokeArgs = param != null ? new[] { param } : new object[] { serializedTransaction };

                            var taskObj = signMethod.Invoke(walletObj, invokeArgs);
                            if (taskObj is Task task)
                            {
                                await task.ConfigureAwait(false);
                                PropertyInfo resultProp = task.GetType().GetProperty("Result");
                                object requestResult = resultProp?.GetValue(task);
                                if (requestResult != null)
                                {
                                    PropertyInfo wasSuccessfulProp = requestResult.GetType().GetProperty("WasSuccessful");
                                    bool wasSuccessful = wasSuccessfulProp != null && (bool)wasSuccessfulProp.GetValue(requestResult);

                                    if (wasSuccessful)
                                    {
                                        PropertyInfo signatureProp = requestResult.GetType().GetProperty("Result");
                                        string signature = signatureProp?.GetValue(requestResult) as string;
                                        return SolanaSdkTransactionResult.Success(signature);
                                    }
                                    else
                                    {
                                        PropertyInfo reasonProp = requestResult.GetType().GetProperty("Reason");
                                        string reason = reasonProp?.GetValue(requestResult) as string;
                                        return SolanaSdkTransactionResult.Failed(reason ?? "Transaction rejected by wallet.");
                                    }
                                }
                            }
                        }
                    }
                }

                return SolanaSdkTransactionResult.Failed("Solana SDK Wallet object is not initialized or connected.");
            }
            catch (Exception ex)
            {
                return SolanaSdkTransactionResult.Failed($"Transaction signing failed in Solana SDK: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        private object GetWeb3Instance()
        {
            if (_web3Type == null) return null;
            PropertyInfo instanceProp = _web3Type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            return instanceProp?.GetValue(null);
        }

        private static string ExtractPublicKey(object accountObj)
        {
            if (accountObj == null) return null;
            PropertyInfo pubKeyProp = accountObj.GetType().GetProperty("PublicKey");
            object pubKeyObj = pubKeyProp?.GetValue(accountObj);
            if (pubKeyObj != null)
            {
                PropertyInfo keyProp = pubKeyObj.GetType().GetProperty("Key");
                if (keyProp != null)
                {
                    return keyProp.GetValue(pubKeyObj) as string;
                }
                return pubKeyObj.ToString();
            }
            return null;
        }

        private string ExtractAuthToken(object web3Instance)
        {
            if (web3Instance == null) return null;
            return null;
        }

        private static object DeserializeTransactionIfRequired(ParameterInfo[] parameters, byte[] payload)
        {
            if (parameters == null || parameters.Length == 0) return payload;
            Type targetType = parameters[0].ParameterType;
            if (targetType == typeof(byte[])) return payload;

            Type txType = FindType("Solana.Unity.Rpc.Models.Transaction");
            if (txType != null)
            {
                MethodInfo deserializeMethod = txType.GetMethod("Deserialize", new[] { typeof(byte[]) });
                if (deserializeMethod != null)
                {
                    return deserializeMethod.Invoke(null, new object[] { payload });
                }
            }

            return null;
        }

        private static Type FindType(string fullTypeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType(fullTypeName, false);
                if (type != null) return type;
            }
            return null;
        }
    }
}
