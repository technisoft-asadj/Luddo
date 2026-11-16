using System.Collections.Generic;

namespace Ludo.Core
{
    /// <summary>
    /// Team Up online: works out who sits where so that people can choose their partner. In a four-seat team match the players facing
    /// each other are partners (seats 0 and 2, seats 1 and 3), so choosing a partner just means being seated across from them.
    /// Every phone runs this on the same room data and gets the same answer.
    /// </summary>
    public static class TeamSeating
    {
        /// <summary>
        /// 'ids' are the people in join order, 'wanted' is the partner each one chose ("" or an unknown id = no choice). Returns the
        /// people (as indexes into 'ids') in seat order. A choice counts when the other person is free, whoever made it; earlier
        /// players are served first. Without any choice the order is unchanged.
        /// </summary>
        public static int[] Arrange(IList<string> ids, IList<string> wanted)
        {
            int n = ids.Count;
            var identity = new int[n];
            for (int i = 0; i < n; i++) identity[i] = i;
            if (n != 4) return identity;                       // a team match is always four people at the table

            var used = new bool[n];
            var pairs = new List<int[]>();
            for (int i = 0; i < n; i++)
            {
                if (used[i]) continue;
                int j = IndexOf(ids, wanted[i], i, used);                     // somebody I asked for
                if (j < 0)
                    for (int k = 0; k < n && j < 0; k++)                      // or somebody who asked for me
                        if (k != i && !used[k] && wanted[k] == ids[i]) j = k;
                if (j < 0) continue;
                used[i] = used[j] = true;
                pairs.Add(new[] { i, j });
            }

            var rest = new List<int>();
            for (int i = 0; i < n; i++) if (!used[i]) rest.Add(i);
            if (rest.Count == 4) { pairs.Add(new[] { rest[0], rest[2] }); pairs.Add(new[] { rest[1], rest[3] }); }   // nobody chose: join order
            else if (rest.Count == 2) pairs.Add(new[] { rest[0], rest[1] });

            pairs.Sort((a, b) => System.Math.Min(a[0], a[1]).CompareTo(System.Math.Min(b[0], b[1])));
            return new[] { pairs[0][0], pairs[1][0], pairs[0][1], pairs[1][1] };
        }

        static int IndexOf(IList<string> ids, string id, int self, bool[] used)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int k = 0; k < ids.Count; k++)
                if (k != self && !used[k] && ids[k] == id) return k;
            return -1;
        }
    }
}
