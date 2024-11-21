using AddinRibbon.Dijkstra;
using System.Collections.Generic;

namespace AddinRibbon.TraysData
{
    public class TraysData
    {
        public static Dictionary<string, List<Node>> CoordinatesData()
        {
            Dictionary<string, List<Node>> sections = new Dictionary<string, List<Node>>();

            // Branch /B001 Nodes
            var b001Nodes = new List<Node>
{
    new Node { SectionName = "/B001", X = 28.737, Y = -13.172, Z = 6.871 },
    new Node { SectionName = "/B001", X = 28.69, Y = -13.166, Z = 1.47 }
};
            sections["/B001"] = b001Nodes;

            // Branch /B101 Nodes
            var b101Nodes = new List<Node>
{
    new Node { SectionName = "/B101", X = 25.696, Y = -13.948, Z = 6.456 },
    new Node { SectionName = "/B101", X = 28.992, Y = -13.95, Z = 6.456 }
};
            sections["/B101"] = b101Nodes;

            // Branch /B102 Nodes
            var b102Nodes = new List<Node>
{
    new Node { SectionName = "/B102", X = 25.846, Y = -19.598, Z = 6.456 },
    new Node { SectionName = "/B102", X = 25.907, Y = -19.29, Z = 6.761 },
    new Node { SectionName = "/B102", X = 5.831, Y = 19.402, Z = 7.556 },
    new Node { SectionName = "/B102", X = 25.871, Y = -13.711, Z = 7.556 },
    new Node { SectionName = "/B102", X = 25.886, Y = -13.702, Z = 6.756 }
};
            sections["/B102"] = b102Nodes;

            // Branch /B104 Nodes
            var b104Nodes = new List<Node>
{
    new Node { SectionName = "/B104", X = 17.789, Y = -20.004, Z = 6.456 },
    new Node { SectionName = "/B104", X = 26.319, Y = -20.026, Z = 6.456 },
    new Node { SectionName = "/B104", X = -26.609, Y = -20.611, Z = 6.456 }
};
            sections["/B104"] = b104Nodes;

            // Branch /B105 Nodes
            var b105Nodes = new List<Node>
{
    new Node { SectionName = "/B105", X = 9.637, Y = -20.059, Z = 8.356 },
    new Node { SectionName = "/B105", X = 9.951, Y = -20.032, Z = 8.326 },
    new Node { SectionName = "/B105", X = 10.655, Y = -20.047, Z = 7.706 },
    new Node { SectionName = "/B105", X = 15.785, Y = -20.055, Z = 7.677 },
    new Node { SectionName = "/B105", X = 17.095, Y = -20.044, Z = 6.456 },
    new Node { SectionName = "/B105", X = 17.789, Y = -20.004, Z = 6.456 }
};
            sections["/B105"] = b105Nodes;

            // Branch /B115 Nodes
            var b115Nodes = new List<Node>
{
    new Node { SectionName = "/B115", X = 9.754, Y = -21.25, Z = 8.356 },
    new Node { SectionName = "/B115", X = 9.865, Y = -23.655, Z = 8.356 },
    new Node { SectionName = "/B115", X = 10.967, Y = -23.926, Z = 8.356 },
    new Node { SectionName = "/B115", X = 11.109, Y = -24.352, Z = 8.356 }
};
            sections["/B115"] = b115Nodes;

            // Branch /B116 Nodes
            var b116Nodes = new List<Node>
{
    new Node { SectionName = "/B116", X = 11.109, Y = -24.352, Z = 8.356 },
    new Node { SectionName = "/B116", X = 11.103, Y = -36.274, Z = 8.356 }
};
            sections["/B116"] = b116Nodes;

            // Branch /B118 Nodes
            var b118Nodes = new List<Node>
{
    new Node { SectionName = "/B118", X = 25.319, Y = -29.598, Z = 5.446 },
    new Node { SectionName = "/B118", X = 26.675, Y = -23.908, Z = 5.575 },
    new Node { SectionName = "/B118", X = 26.675, Y = -26.746, Z = 5.575 },
    new Node { SectionName = "/B118", X = 26.675, Y = -27.375, Z = 5.446 },
    new Node { SectionName = "/B118", X = 26.675, Y = -28.995, Z = 5.446 },
    new Node { SectionName = "/B118", X = 25.319, Y = -28.995, Z = 5.446 }
};
            sections["/B118"] = b118Nodes;

            // Branch /B119 Nodes
            var b119Nodes = new List<Node>
{
    new Node { SectionName = "/B119", X = 21.491, Y = -38.051, Z = 5.446 },
    new Node { SectionName = "/B119", X = 24.641, Y = -38.051, Z = 5.446 },
    new Node { SectionName = "/B119", X = 25.319, Y = -29.598, Z = 5.446 },
    new Node { SectionName = "/B119", X = 25.319, Y = -31.259, Z = 5.446 },
    new Node { SectionName = "/B119", X = 25.319, Y = -32.673, Z = 5.446 },
    new Node { SectionName = "/B119", X = 25.319, Y = -32.673, Z = 5.446 },
    new Node { SectionName = "/B119", X = 25.319, Y = -38.051, Z = 5.446 },
    new Node { SectionName = "/B119", X = 24.641, Y = -38.051, Z = 5.446 }
};
            sections["/B119"] = b119Nodes;

            // Branch /B143 Nodes
            var b143Nodes = new List<Node>
{
    new Node { SectionName = "/B143", X = 11.391, Y = -40.134, Z = 8.27 },
    new Node { SectionName = "/B143", X = 11.338, Y = -40.135, Z = 5.027 },
    new Node { SectionName = "/B143", X = 7.319, Y = -40.026, Z = 4.946 },
    new Node { SectionName = "/B143", X = 7.186, Y = -37.643, Z = 4.946 }
};
            sections["/B143"] = b143Nodes;

            // Branch /B144 Nodes
            var b144Nodes = new List<Node>
{
    new Node { SectionName = "/B144", X = 11.17, Y = -36.553, Z = 8.356 },
    new Node { SectionName = "/B144", X = 11.17, Y = -40.148, Z = 8.363 }
};
            sections["/B144"] = b144Nodes;

            // Branch /BC003 Nodes
            var bc003Nodes = new List<Node>
{
    new Node { SectionName = "/BC003", X = 0.795, Y = -22.105, Z = 8.056 },
    new Node { SectionName = "/BC003", X = 9.791, Y = -22.077, Z = 8.056 }
};
            sections["/BC003"] = bc003Nodes;

            // Branch /BC004 Nodes
            var bc004Nodes = new List<Node>
{
    new Node { SectionName = "/BC004", X = 0.841, Y = -21.139, Z = 4.568 },
    new Node { SectionName = "/BC004", X = 0.86, Y = -21.186, Z = 8.019 },
    new Node { SectionName = "/BC004", X = 0.815, Y = -21.88, Z = 8.027 }
};
            sections["/BC004"] = bc004Nodes;

            // Branch /BC106 Nodes
            var bc106Nodes = new List<Node>
{
    new Node { SectionName = "/BC106", X = 21.509, Y = -38.093, Z = 5.177 },
    new Node { SectionName = "/BC106", X = 21.338, Y = -38.14, Z = 5.177 },
    new Node { SectionName = "/BC106", X = 21.002, Y = -38.103, Z = 5.327 },
    new Node { SectionName = "/BC106", X = 19.925, Y = -38.07, Z = 5.327 },
    new Node { SectionName = "/BC106", X = 20.05, Y = -37.602, Z = 5.327 },
    new Node { SectionName = "/BC106", X = 17.965, Y = -38.062, Z = 5.315 },
    new Node { SectionName = "/BC106", X = 17.622, Y = -37.82, Z = 5.327 },
    new Node { SectionName = "/BC106", X = 17.656, Y = -36.598, Z = 5.327 },
    new Node { SectionName = "/BC106", X = 18.156, Y = -36.716, Z = 5.327 },
    new Node { SectionName = "/BC106", X = 17.616, Y = -36.246, Z = 5.327 }
};
            sections["/BC106"] = bc106Nodes;

            // Branch /BC107 Nodes
            var bc107Nodes = new List<Node>
{
    new Node { SectionName = "/BC107", X = 17.604, Y = -36.023, Z = 5.327 },
    new Node { SectionName = "/BC107", X = 17.59, Y = -34.815, Z = 5.327 },
    new Node { SectionName = "/BC107", X = 17.629, Y = -33.623, Z = 5.327 }
};
            sections["/BC107"] = bc107Nodes;

            // Branch /BC108 Nodes
            var bc108Nodes = new List<Node>
{
    new Node { SectionName = "/BC108", X = 17.626, Y = -33.184, Z = 5.327 },
    new Node { SectionName = "/BC108", X = 17.66, Y = -30.877, Z = 5.327 },
    new Node { SectionName = "/BC108", X = 18.333, Y = -30.525, Z = 5.327 },
    new Node { SectionName = "/BC108", X = 18.854, Y = -30.558, Z = 4.927 },
    new Node { SectionName = "/BC108", X = 19.41, Y = -30.445, Z = 4.927 },
    new Node { SectionName = "/BC108", X = 19.495, Y = -26.523, Z = 4.928 },
    new Node { SectionName = "/BC108", X = 18.987, Y = -26.733, Z = 4.927 },
    new Node { SectionName = "/BC108", X = 20.064, Y = -26.738, Z = 4.927 }
};
            sections["/BC108"] = bc108Nodes;

            // Branch /BC109 Nodes
            var bc109Nodes = new List<Node>
{
    new Node { SectionName = "/BC109", X = 18.847, Y = -26.523, Z = 4.904 },
    new Node { SectionName = "/BC109", X = 17.932, Y = -26.709, Z = 4.927 },
    new Node { SectionName = "/BC109", X = 17.601, Y = -26.472, Z = 4.927 },
    new Node { SectionName = "/BC109", X = 17.618, Y = -24.92, Z = 4.927 }
};
            sections["/BC109"] = bc109Nodes;

            // Branch /BC110 Nodes
            var bc110Nodes = new List<Node>
{
    new Node { SectionName = "/BC110", X = 20.219, Y = -26.742, Z = 4.927 },
    new Node { SectionName = "/BC110", X = 20.671, Y = -26.988, Z = 4.927 },
    new Node { SectionName = "/BC110", X = 21.883, Y = -29.029, Z = 4.927 },
    new Node { SectionName = "/BC110", X = 24.283, Y = -29.053, Z = 4.927 },
    new Node { SectionName = "/BC110", X = 24.721, Y = -29.043, Z = 5.159 },
    new Node { SectionName = "/BC110", X = 25.002, Y = 89.04, Z = 5.177 }
};
            sections["/BC110"] = bc110Nodes;

            // Branch /BC113 Nodes
            var bc113Nodes = new List<Node>
{
    new Node { SectionName = "/BC113", X = 18.164, Y = -36.726, Z = 5.32 },
    new Node { SectionName = "/BC113", X = 19.566, Y = -36.778, Z = 5.327 }
};
            sections["/BC113"] = bc113Nodes;

            // Branch /BC114 Nodes
            var bc114Nodes = new List<Node>
{
    new Node { SectionName = "/BC114", X = 20.109, Y = -37.394, Z = 5.324 },
    new Node { SectionName = "/BC114", X = 20.089, Y = -36.608, Z = 5.32 },
    new Node { SectionName = "/BC114", X = 20.095, Y = -30.038, Z = 5.319 }
};
            sections["/BC114"] = bc114Nodes;

            // Branch /BC166 Nodes
            var bc166Nodes = new List<Node>
{
    new Node { SectionName = "/BC166", X = 0.851, Y = -23.604, Z = 3.059 },
    new Node { SectionName = "/BC166", X = 0.831, Y = -22.255, Z = 2.981 },
    new Node { SectionName = "/BC166", X = 0.856, Y = -21.204, Z = 3.067 },
    new Node { SectionName = "/BC166", X = 0.803, Y = -21.137, Z = 4.221 }
};
            sections["/BC166"] = bc166Nodes;

            // Branch /BC167 Nodes
            var bc167Nodes = new List<Node>
{
    new Node { SectionName = "/BC167", X = 11.709, Y = -23.706, Z = 7.877 },
    new Node { SectionName = "/BC167", X = 11.709, Y = -23.705, Z = 3.681 }
};
            sections["/BC167"] = bc167Nodes;

            // Branch /BC168 Nodes
            var bc168Nodes = new List<Node>
{
    new Node { SectionName = "/BC168", X = 11.709, Y = -36.241, Z = 7.888 },
    new Node { SectionName = "/BC168", X = 11.709, Y = -36.244, Z = 3.667 }
};
            sections["/BC168"] = bc168Nodes;

            // Branch /C001 Nodes
            var c001Nodes = new List<Node>
{
    new Node { SectionName = "/C001", X = 27.935, Y = -13.172, Z = 6.271 },
    new Node { SectionName = "/C001", X = 27.879, Y = -13.166, Z = 1.442 }
};
            sections["/C001"] = c001Nodes;

            // Branch /C004 Nodes
            var c004Nodes = new List<Node>
{
    new Node { SectionName = "/C004", X = 9.808, Y = -21.263, Z = -7.856 },
    new Node { SectionName = "/C004", X = 9.893, Y = -23.571, Z = 7.856 },
    new Node { SectionName = "/C004", X = 11.051, Y = -23.755, Z = 7.856 },
    new Node { SectionName = "/C004", X = 11.174, Y = -24.127, Z = 7.85 }
};
            sections["/C004"] = c004Nodes;

            // Branch /C005 Nodes
            var c005Nodes = new List<Node>
{
    new Node { SectionName = "/C005", X = 11.174, Y = -24.127, Z = 7.85 },
    new Node { SectionName = "/C005", X = 11.161, Y = -36.033, Z = 7.856 }
};
            sections["/C005"] = c005Nodes;

            // Branch /C006 Nodes
            var c006Nodes = new List<Node>
{
    new Node { SectionName = "/C006", X = 11.104, Y = -40.494, Z = 5.446 },
    new Node { SectionName = "/C006", X = 11.02, Y = -41.065, Z = 5.446 },
    new Node { SectionName = "/C006", X = 0.535, Y = -41.153, Z = 5.446 }
};
            sections["/C006"] = c006Nodes;

            // Branch /C011 Nodes
            var c011Nodes = new List<Node>
{
    new Node { SectionName = "/C011", X = 11.161, Y = -36.033, Z = 7.856 },
    new Node { SectionName = "/C011", X = 11.161, Y = -40.305, Z = 7.87 }
};
            sections["/C011"] = c011Nodes;

            // Branch /C102 Nodes
            var c102Nodes = new List<Node>
{
    new Node { SectionName = "/C102", X = 26.607, Y = -20.365, Z = 5.956 },
    new Node { SectionName = "/C102", X = 26.553, Y = -19.018, Z = 6.043 },
    new Node { SectionName = "/C102", X = 26.554, Y = -15.125, Z = 7.556 },
    new Node { SectionName = "/C102", X = 26.593, Y = -14.323, Z = 7.556 },
    new Node { SectionName = "/C102", X = 26.583, Y = -14.29, Z = 6.188 }
};
            sections["/C102"] = c102Nodes;

            // Branch /C104 Nodes
            var c104Nodes = new List<Node>
{
    new Node { SectionName = "/C104", X = 17.794, Y = -20.145, Z = 5.956 },
    new Node { SectionName = "/C104", X = 26.051, Y = -20.128, Z = 5.956 }
};
            sections["/C104"] = c104Nodes;

            // Branch /C105 Nodes
            var c105Nodes = new List<Node>
{
    new Node { SectionName = "/C105", X = 9.528, Y = -20.133, Z = 7.856 },
    new Node { SectionName = "/C105", X = 10.54, Y = -20.149, Z = 7.206 },
    new Node { SectionName = "/C105", X = 15.635, Y = -20.131, Z = 7.177 },
    new Node { SectionName = "/C105", X = 16.928, Y = -20.133, Z = 5.956 },
    new Node { SectionName = "/C105", X = 17.794, Y = -20.145, Z = 5.956 }
};
            sections["/C105"] = c105Nodes;

            // Branch /C118 Nodes
            var c118Nodes = new List<Node>
{
    new Node { SectionName = "/C118", X = 26.675, Y = -20.908, Z = 5.925 },
    new Node { SectionName = "/C118", X = 26.675, Y = -20.908, Z = 5.525 },
    new Node { SectionName = "/C118", X = 26.675, Y = -20.908, Z = 5.525 }
};
            sections["/C118"] = c118Nodes;

            // Branch /C119 Nodes
            var c119Nodes = new List<Node>
{
    new Node { SectionName = "/C119", X = 26.675, Y = -23.908, Z = 5.525 },
    new Node { SectionName = "/C119", X = 26.675, Y = -26.746, Z = 5.525 },
    new Node { SectionName = "/C119", X = 26.675, Y = -27.375, Z = 4.896 },
    new Node { SectionName = "/C119", X = 26.675, Y = -28.995, Z = 4.896 },
    new Node { SectionName = "/C119", X = 25.319, Y = -28.995, Z = 4.896 },
    new Node { SectionName = "/C119", X = 25.319, Y = -29.598, Z = 4.896 }
};
            sections["/C119"] = c119Nodes;

            // Branch /C120 Nodes
            var c120Nodes = new List<Node>
{
    new Node { SectionName = "/C120", X = 25.319, Y = -29.598, Z = 4.896 },
    new Node { SectionName = "/C120", X = 25.319, Y = -31.259, Z = 4.896 },
    new Node { SectionName = "/C120", X = 25.319, Y = -32.673, Z = 4.896 }
};
            sections["/C120"] = c120Nodes;

            // Branch /C121 Nodes
            var c121Nodes = new List<Node>
{
    new Node { SectionName = "/C121", X = 25.319, Y = -32.673, Z = 4.896 },
    new Node { SectionName = "/C121", X = 25.319, Y = -38.051, Z = 4.896 },
    new Node { SectionName = "/C121", X = 24.641, Y = -38.051, Z = 4.896 }
};
            sections["/C121"] = c121Nodes;

            // Branch /C122 Nodes
            var c122Nodes = new List<Node>
{
    new Node { SectionName = "/C122", X = 24.641, Y = -38.051, Z = 4.896 },
    new Node { SectionName = "/C122", X = 21.491, Y = -38.051, Z = 4.896 }
};
            sections["/C122"] = c122Nodes;

            // Branch /C141 Nodes
            var c141Nodes = new List<Node>
{
    new Node { SectionName = "/C141", X = 10.951, Y = -40.164, Z = 7.774 },
    new Node { SectionName = "/C141", X = 10.893, Y = -40.143, Z = 5.531 },
    new Node { SectionName = "/C141", X = 7.3, Y = -40.036, Z = 5.446 },
    new Node { SectionName = "/C141", X = 7.182, Y = -37.654, Z = 5.446 }
};
            sections["/C141"] = c141Nodes;

            // Branch /DFB001 Nodes
            var dfb001Nodes = new List<Node>
{
    new Node { SectionName = "/DFB001", X = 31.627, Y = -9.055, Z = 1.511 },
    new Node { SectionName = "/DFB001", X = 33.33, Y = -9.065, Z = 1.511 },
    new Node { SectionName = "/DFB001", X = 32.72, Y = -9.087, Z = 1.511 }
};
            sections["/DFB001"] = dfb001Nodes;

            // Branch /DFB014 Nodes
            var dfb014Nodes = new List<Node>
{
    new Node { SectionName = "/DFB014", X = 28.922, Y = -9.012, Z = 1.511 },
    new Node { SectionName = "/DFB014", X = 31.627, Y = -9.055, Z = 1.511 }
};
            sections["/DFB014"] = dfb014Nodes;

            // Branch /DFB015 Nodes
            var dfb015Nodes = new List<Node>
{
    new Node { SectionName = "/DFB015", X = 28.725, Y = -12.459, Z = 1.511 },
    new Node { SectionName = "/DFB015", X = 28.693, Y = -10.635, Z = 1.511 },
    new Node { SectionName = "/DFB015", X = 28.975, Y = -9.557, Z = 1.511 }
};
            sections["/DFB015"] = dfb015Nodes;

            // Branch /DFC006 Nodes
            var dfc006Nodes = new List<Node>
{
    new Node { SectionName = "/DFC006", X = 28.063, Y = -12.083, Z = 1.211 },
    new Node { SectionName = "/DFC006", X = 33.158, Y = -12.017, Z = 1.211 },
    new Node { SectionName = "/DFC006", X = -34.077, Y = -12.009, Z = 1.211 }
};
            sections["/DFC006"] = dfc006Nodes;

            // Branch /DFC006-1 Nodes
            var dfc006_1Nodes = new List<Node>
{
    new Node { SectionName = "/DFC006-1", X = 33.129, Y = -11.653, Z = 1.211 },
    new Node { SectionName = "/DFC006-1", X = 33.087, Y = -10.439, Z = 1.211 }
};
            sections["/DFC006-1"] = dfc006_1Nodes;

            // Branch /DFC006-2 Nodes
            var dfc006_2Nodes = new List<Node>
{
    new Node { SectionName = "/DFC006-2", X = 34.005, Y = 11.652, Z = 1.211 },
    new Node { SectionName = "/DFC006-2", X = 33.99, Y = -11.045, Z = 1.211 }
};
            sections["/DFC006-2"] = dfc006_2Nodes;

            return sections;
        }   


        public static List<Node> GetBNodes()
        {
            var allSections = CoordinatesData();
            var bBranches = new List<Node>();
            foreach (var section in allSections)
            {
                if (section.Key.StartsWith("/B"))
                {
                    bBranches.AddRange(section.Value);
                }
            }
            return bBranches;
        }

        public static List<Node> GetBCBranches()
        {
            var allSections = CoordinatesData();
            var bcBranches = new List<Node>();
            foreach (var section in allSections)
            {
                if (section.Key.StartsWith("/BC"))
                {
                    bcBranches.AddRange(section.Value);
                }
            }
            return bcBranches;
        }

        public static List<Node> GetCBranches()
        {
            var allSections = CoordinatesData();
            var cBranches = new List<Node>();
            foreach (var section in allSections)
            {
                if (section.Key.StartsWith("/C"))
                {
                    cBranches.AddRange(section.Value);
                }
            }
            return cBranches;
        }

        public static List<Node> GetDFBBranches()
        {
            var allSections = CoordinatesData();
            var dfBranches = new List<Node>();
            foreach (var section in allSections)
            {
                if (section.Key.StartsWith("/DFB"))
                {
                    dfBranches.AddRange(section.Value);
                }
            }
            return dfBranches;
        }

        public static List<Node> GetDFCBranches()
        {
            var allSections = CoordinatesData();
            var dfBranches = new List<Node>();
            foreach (var section in allSections)
            {
                if (section.Key.StartsWith("/DFC"))
                {
                    dfBranches.AddRange(section.Value);
                }
            }
            return dfBranches;
        }
    }
}