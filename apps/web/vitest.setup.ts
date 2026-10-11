if (typeof Buffer !== "undefined" && typeof Uint8Array !== "undefined") {
  Object.setPrototypeOf(Buffer.prototype, Uint8Array.prototype);
}
