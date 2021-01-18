using System;
using System.Collections.Generic;

namespace Chika.GameCore
{
    public class GameAccountPool
    {
        public static GameClient standaloneGameClientInstance = null;

        public static List<GameClient> gameClientPool = new();

        public static Random random = new Random();
    }
}
