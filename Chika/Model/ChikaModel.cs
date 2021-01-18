using System;
using System.Collections.Generic;

namespace Chika.Model
{
    public class ChikaModel
    {
        public class ChikaUpdateFullData
        {
            public List<ChikaUpdateData> chikaData;
            public long time;
        }
        public class ChikaUpdateData
        {
            public long qq;

            public int arena_rank_before;
            public int arena_rank_after;

            public int grand_arena_rank_before;
            public int grand_arena_rank_after;

            public List<long> groups;
        }

        public class ChikaUpdateQQRequest
        {
            public long Qq { get; set; }
            public long ViewerId { get; set; }
            public long Group { get; set; }
        }

        public class ChikaGetLogResponse
        {
            public int Ret { get; set; } = 0;
            public long ViewerId { get; set; }
            public List<ChikaLog> Logs { get; set; }
            public class ChikaLog
            {
                public DateTime Time {get; set;}
                public int A_before { get; set; }
                public int A_after { get; set; }
                public int Ga_before { get; set; }
                public int Ga_after { get; set; }
            }
        }

    }
}
