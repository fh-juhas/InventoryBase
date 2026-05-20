namespace InventoryBase.Web.ViewModels
{
    public class TabulatorRequest
    {
        public int page { get; set; } = 1;
        public int size { get; set; } = 20;
        public string? field { get; set; }   // sort field
        public string? dir { get; set; }   // "asc" | "desc"

        public string? search { get; set; }
        public string? status { get; set; }
        public string? category { get; set; }
        public string? role { get; set; }
        public int? month { get; set; }
        public int? year { get; set; }
    }

    public class TabulatorResponse<T>
    {
        public int last_page { get; set; }
        public IList<T> data { get; set; } = new List<T>();
    }

}
