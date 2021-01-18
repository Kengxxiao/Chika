namespace Chika.Model
{
    public class Profile
    {
        public string SignVerify { get; set; } = "";
        public bool NoClient { get; set; }
        public bool HasValue { get; set; } = false;

        public int Arena_rank { get; set; }
        public int Arena_group { get; set; }
        public long Arena_time { get; set; }

        public int Grand_arena_rank { get; set; }
        public int Grand_arena_group { get; set; }
        public long Grand_arena_time { get; set; }

    }
}
