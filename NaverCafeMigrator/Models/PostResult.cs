namespace NaverCafeMigrator.Models
{
    public class PostResult
    {
        public int TotalPosts { get; set; } = 0;
        public int SuccessfulPosts { get; set; } = 0;
        public int FailedPosts { get; set; } = 0;
        public string Message { get; set; } = string.Empty;
        public List<PostItem> Items { get; set; } = new List<PostItem>();
    }
}
