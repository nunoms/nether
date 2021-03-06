// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Nether.Data.Leaderboard;

namespace Nether.Web.Features.Leaderboard
{
    public class LeaderboardGetResponseModel
    {
        public List<LeaderboardEntry> Entries { get; set; }

        public class LeaderboardEntry
        {
            public static implicit operator LeaderboardEntry(GameScore score)
            {
                return new LeaderboardEntry { Gamertag = score.GamerTag, Score = score.Score };
            }

            /// <summary>
            /// Gamertag
            /// </summary>
            public string Gamertag { get; set; }

            /// <summary>
            /// Scores
            /// </summary>
            public int Score { get; set; }
        }
    }
}