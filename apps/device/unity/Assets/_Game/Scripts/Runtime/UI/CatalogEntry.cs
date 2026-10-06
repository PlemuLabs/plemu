namespace Plemu.App
{
    public enum ContentLane
    {
        Signed,
        Local
    }

    /// <summary>
    /// A minimal, display-ready view of one playable title drawn from the verified manifest or local lane:
    /// holds identity, metadata, content lane, and platform eligibility flags.
    /// </summary>
    public sealed class CatalogEntry
    {
        public string Id { get; }
        public string Title { get; }
        public string System { get; }
        public string License { get; }
        public ContentLane Lane { get; }
        public bool CanTip { get; }
        public bool CanMint { get; }
        public bool CanPromote { get; }
        public string CreatorWallet { get; }

        public CatalogEntry(
            string id,
            string title,
            string system,
            string license,
            ContentLane lane = ContentLane.Signed,
            bool canTip = true,
            bool canMint = true,
            bool canPromote = true,
            string creatorWallet = null)
        {
            Id = id;
            Title = title;
            System = system;
            License = license;
            Lane = lane;
            CanTip = canTip;
            CanMint = canMint;
            CanPromote = canPromote;
            CreatorWallet = creatorWallet;
        }
    }
}
