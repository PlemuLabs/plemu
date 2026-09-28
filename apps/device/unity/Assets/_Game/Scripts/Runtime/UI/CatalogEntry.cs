namespace Plemu.App
{
    /// <summary>
    /// A minimal, display-ready view of one playable title drawn from the verified manifest: just
    /// what the Library and Detail screens render (id, title, system, license badge).
    /// </summary>
    public sealed class CatalogEntry
    {
        public string Id { get; }
        public string Title { get; }
        public string System { get; }
        public string License { get; }

        public CatalogEntry(string id, string title, string system, string license)
        {
            Id = id;
            Title = title;
            System = system;
            License = license;
        }
    }
}
