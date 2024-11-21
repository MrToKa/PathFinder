using AddinRibbon.Dijkstra;
using System.Collections.Generic;

namespace AddinRibbon.TraysData
{
    public class TraysData
    {
        public static Dictionary<string, List<Node>> CoordinatesData()
        {
            Dictionary<string, List<Node>> trays = new Dictionary<string, List<Node>>();

            // Tray /B001 Nodes
            var b001Nodes = new List<Node>
{
    new Node { TrayName = "/B001", X = 28.737, Y = -13.172, Z = 6.871 },
    new Node { TrayName = "/B001", X = 28.69, Y = -13.166, Z = 1.47 }
};
            trays["/B001"] = b001Nodes;

            // Tray /B101 Nodes
            var b101Nodes = new List<Node>
{
    new Node { TrayName = "/B101", X = 25.696, Y = -13.948, Z = 6.456 },
    new Node { TrayName = "/B101", X = 28.992, Y = -13.95, Z = 6.456 }
};
            trays["/B101"] = b101Nodes;

            // Tray /B102 Nodes
            var b102Nodes = new List<Node>
{
    new Node { TrayName = "/B102", X = 25.846, Y = -19.598, Z = 6.456 },
    new Node { TrayName = "/B102", X = 25.907, Y = -19.29, Z = 6.761 },
    new Node { TrayName = "/B102", X = 5.831, Y = 19.402, Z = 7.556 },
    new Node { TrayName = "/B102", X = 25.871, Y = -13.711, Z = 7.556 },
    new Node { TrayName = "/B102", X = 25.886, Y = -13.702, Z = 6.756 }
};
            trays["/B102"] = b102Nodes;

            // Tray /B104 Nodes
            var b104Nodes = new List<Node>
{
    new Node { TrayName = "/B104", X = 17.789, Y = -20.004, Z = 6.456 },
    new Node { TrayName = "/B104", X = 26.319, Y = -20.026, Z = 6.456 },
    new Node { TrayName = "/B104", X = -26.609, Y = -20.611, Z = 6.456 }
};
            trays["/B104"] = b104Nodes;

            // Tray /B105 Nodes
            var b105Nodes = new List<Node>
{
    new Node { TrayName = "/B105", X = 9.637, Y = -20.059, Z = 8.356 },
    new Node { TrayName = "/B105", X = 9.951, Y = -20.032, Z = 8.326 },
    new Node { TrayName = "/B105", X = 10.655, Y = -20.047, Z = 7.706 },
    new Node { TrayName = "/B105", X = 15.785, Y = -20.055, Z = 7.677 },
    new Node { TrayName = "/B105", X = 17.095, Y = -20.044, Z = 6.456 },
    new Node { TrayName = "/B105", X = 17.789, Y = -20.004, Z = 6.456 }
};
            trays["/B105"] = b105Nodes;

            // Tray /B115 Nodes
            var b115Nodes = new List<Node>
{
    new Node { TrayName = "/B115", X = 9.754, Y = -21.25, Z = 8.356 },
    new Node { TrayName = "/B115", X = 9.865, Y = -23.655, Z = 8.356 },
    new Node { TrayName = "/B115", X = 10.967, Y = -23.926, Z = 8.356 },
    new Node { TrayName = "/B115", X = 11.109, Y = -24.352, Z = 8.356 }
};
            trays["/B115"] = b115Nodes;

            // Tray /B116 Nodes
            var b116Nodes = new List<Node>
{
    new Node { TrayName = "/B116", X = 11.109, Y = -24.352, Z = 8.356 },
    new Node { TrayName = "/B116", X = 11.103, Y = -36.274, Z = 8.356 }
};
            trays["/B116"] = b116Nodes;

            // Tray /B118 Nodes
            var b118Nodes = new List<Node>
{
    new Node { TrayName = "/B118", X = 25.319, Y = -29.598, Z = 5.446 },
    new Node { TrayName = "/B118", X = 26.675, Y = -23.908, Z = 5.575 },
    new Node { TrayName = "/B118", X = 26.675, Y = -26.746, Z = 5.575 },
    new Node { TrayName = "/B118", X = 26.675, Y = -27.375, Z = 5.446 },
    new Node { TrayName = "/B118", X = 26.675, Y = -28.995, Z = 5.446 },
    new Node { TrayName = "/B118", X = 25.319, Y = -28.995, Z = 5.446 }
};
            trays["/B118"] = b118Nodes;

            // Tray /B119 Nodes
            var b119Nodes = new List<Node>
{
    new Node { TrayName = "/B119", X = 21.491, Y = -38.051, Z = 5.446 },
    new Node { TrayName = "/B119", X = 24.641, Y = -38.051, Z = 5.446 },
    new Node { TrayName = "/B119", X = 25.319, Y = -29.598, Z = 5.446 },
    new Node { TrayName = "/B119", X = 25.319, Y = -31.259, Z = 5.446 },
    new Node { TrayName = "/B119", X = 25.319, Y = -32.673, Z = 5.446 },
    new Node { TrayName = "/B119", X = 25.319, Y = -32.673, Z = 5.446 },
    new Node { TrayName = "/B119", X = 25.319, Y = -38.051, Z = 5.446 },
    new Node { TrayName = "/B119", X = 24.641, Y = -38.051, Z = 5.446 }
};
            trays["/B119"] = b119Nodes;

            // Tray /B143 Nodes
            var b143Nodes = new List<Node>
{
    new Node { TrayName = "/B143", X = 11.391, Y = -40.134, Z = 8.27 },
    new Node { TrayName = "/B143", X = 11.338, Y = -40.135, Z = 5.027 },
    new Node { TrayName = "/B143", X = 7.319, Y = -40.026, Z = 4.946 },
    new Node { TrayName = "/B143", X = 7.186, Y = -37.643, Z = 4.946 }
};
            trays["/B143"] = b143Nodes;

            // Tray /B144 Nodes
            var b144Nodes = new List<Node>
{
    new Node { TrayName = "/B144", X = 11.17, Y = -36.553, Z = 8.356 },
    new Node { TrayName = "/B144", X = 11.17, Y = -40.148, Z = 8.363 }
};
            trays["/B144"] = b144Nodes;

            // Tray /BC003 Nodes
            var bc003Nodes = new List<Node>
{
    new Node { TrayName = "/BC003", X = 0.795, Y = -22.105, Z = 8.056 },
    new Node { TrayName = "/BC003", X = 9.791, Y = -22.077, Z = 8.056 }
};
            trays["/BC003"] = bc003Nodes;

            // Tray /BC004 Nodes
            var bc004Nodes = new List<Node>
{
    new Node { TrayName = "/BC004", X = 0.841, Y = -21.139, Z = 4.568 },
    new Node { TrayName = "/BC004", X = 0.86, Y = -21.186, Z = 8.019 },
    new Node { TrayName = "/BC004", X = 0.815, Y = -21.88, Z = 8.027 }
};
            trays["/BC004"] = bc004Nodes;

            // Tray /BC106 Nodes
            var bc106Nodes = new List<Node>
{
    new Node { TrayName = "/BC106", X = 21.509, Y = -38.093, Z = 5.177 },
    new Node { TrayName = "/BC106", X = 21.338, Y = -38.14, Z = 5.177 },
    new Node { TrayName = "/BC106", X = 21.002, Y = -38.103, Z = 5.327 },
    new Node { TrayName = "/BC106", X = 19.925, Y = -38.07, Z = 5.327 },
    new Node { TrayName = "/BC106", X = 20.05, Y = -37.602, Z = 5.327 },
    new Node { TrayName = "/BC106", X = 17.965, Y = -38.062, Z = 5.315 },
    new Node { TrayName = "/BC106", X = 17.622, Y = -37.82, Z = 5.327 },
    new Node { TrayName = "/BC106", X = 17.656, Y = -36.598, Z = 5.327 },
    new Node { TrayName = "/BC106", X = 18.156, Y = -36.716, Z = 5.327 },
    new Node { TrayName = "/BC106", X = 17.616, Y = -36.246, Z = 5.327 }
};
            trays["/BC106"] = bc106Nodes;

            // Tray /BC107 Nodes
            var bc107Nodes = new List<Node>
{
    new Node { TrayName = "/BC107", X = 17.604, Y = -36.023, Z = 5.327 },
    new Node { TrayName = "/BC107", X = 17.59, Y = -34.815, Z = 5.327 },
    new Node { TrayName = "/BC107", X = 17.629, Y = -33.623, Z = 5.327 }
};
            trays["/BC107"] = bc107Nodes;

            // Tray /BC108 Nodes
            var bc108Nodes = new List<Node>
{
    new Node { TrayName = "/BC108", X = 17.626, Y = -33.184, Z = 5.327 },
    new Node { TrayName = "/BC108", X = 17.66, Y = -30.877, Z = 5.327 },
    new Node { TrayName = "/BC108", X = 18.333, Y = -30.525, Z = 5.327 },
    new Node { TrayName = "/BC108", X = 18.854, Y = -30.558, Z = 4.927 },
    new Node { TrayName = "/BC108", X = 19.41, Y = -30.445, Z = 4.927 },
    new Node { TrayName = "/BC108", X = 19.495, Y = -26.523, Z = 4.928 },
    new Node { TrayName = "/BC108", X = 18.987, Y = -26.733, Z = 4.927 },
    new Node { TrayName = "/BC108", X = 20.064, Y = -26.738, Z = 4.927 }
};
            trays["/BC108"] = bc108Nodes;

            // Tray /BC109 Nodes
            var bc109Nodes = new List<Node>
{
    new Node { TrayName = "/BC109", X = 18.847, Y = -26.523, Z = 4.904 },
    new Node { TrayName = "/BC109", X = 17.932, Y = -26.709, Z = 4.927 },
    new Node { TrayName = "/BC109", X = 17.601, Y = -26.472, Z = 4.927 },
    new Node { TrayName = "/BC109", X = 17.618, Y = -24.92, Z = 4.927 }
};
            trays["/BC109"] = bc109Nodes;

            // Tray /BC110 Nodes
            var bc110Nodes = new List<Node>
{
    new Node { TrayName = "/BC110", X = 20.219, Y = -26.742, Z = 4.927 },
    new Node { TrayName = "/BC110", X = 20.671, Y = -26.988, Z = 4.927 },
    new Node { TrayName = "/BC110", X = 21.883, Y = -29.029, Z = 4.927 },
    new Node { TrayName = "/BC110", X = 24.283, Y = -29.053, Z = 4.927 },
    new Node { TrayName = "/BC110", X = 24.721, Y = -29.043, Z = 5.159 },
    new Node { TrayName = "/BC110", X = 25.002, Y = 89.04, Z = 5.177 }
};
            trays["/BC110"] = bc110Nodes;

            // Tray /BC113 Nodes
            var bc113Nodes = new List<Node>
{
    new Node { TrayName = "/BC113", X = 18.164, Y = -36.726, Z = 5.32 },
    new Node { TrayName = "/BC113", X = 19.566, Y = -36.778, Z = 5.327 }
};
            trays["/BC113"] = bc113Nodes;

            // Tray /BC114 Nodes
            var bc114Nodes = new List<Node>
{
    new Node { TrayName = "/BC114", X = 20.109, Y = -37.394, Z = 5.324 },
    new Node { TrayName = "/BC114", X = 20.089, Y = -36.608, Z = 5.32 },
    new Node { TrayName = "/BC114", X = 20.059, Y = -31.816, Z = 5.327 },
    new Node { TrayName = "/BC114", X = 20.089, Y = -33.024, Z = 5.327 },
    new Node { TrayName = "/BC114", X = 20.089, Y = -34.230, Z = 5.32 },
    new Node { TrayName = "/BC114", X = 20.089, Y = -35.403, Z = 5.32 },
    new Node { TrayName = "/BC114", X = 20.089, Y = -36.578, Z = 5.32 },
    new Node { TrayName = "/BC114", X = 20.095, Y = -30.038, Z = 5.319 }
};
            trays["/BC114"] = bc114Nodes;

            // Tray /BC166 Nodes
            var bc166Nodes = new List<Node>
{
    new Node { TrayName = "/BC166", X = 0.851, Y = -23.604, Z = 3.059 },
    new Node { TrayName = "/BC166", X = 0.831, Y = -22.255, Z = 2.981 },
    new Node { TrayName = "/BC166", X = 0.856, Y = -21.204, Z = 3.067 },
    new Node { TrayName = "/BC166", X = 0.803, Y = -21.137, Z = 4.221 }
};
            trays["/BC166"] = bc166Nodes;

            // Tray /BC167 Nodes
            var bc167Nodes = new List<Node>
{
    new Node { TrayName = "/BC167", X = 11.709, Y = -23.706, Z = 7.877 },
    new Node { TrayName = "/BC167", X = 11.709, Y = -23.705, Z = 3.681 }
};
            trays["/BC167"] = bc167Nodes;

            // Tray /BC168 Nodes
            var bc168Nodes = new List<Node>
{
    new Node { TrayName = "/BC168", X = 11.709, Y = -36.241, Z = 7.888 },
    new Node { TrayName = "/BC168", X = 11.709, Y = -36.244, Z = 3.667 }
};
            trays["/BC168"] = bc168Nodes;

            // Tray /C001 Nodes
            var c001Nodes = new List<Node>
{
    new Node { TrayName = "/C001", X = 27.935, Y = -13.172, Z = 6.271 },
    new Node { TrayName = "/C001", X = 27.879, Y = -13.166, Z = 1.442 }
};
            trays["/C001"] = c001Nodes;

            // Tray /C004 Nodes
            var c004Nodes = new List<Node>
{
    new Node { TrayName = "/C004", X = 9.808, Y = -21.263, Z = -7.856 },
    new Node { TrayName = "/C004", X = 9.893, Y = -23.571, Z = 7.856 },
    new Node { TrayName = "/C004", X = 11.051, Y = -23.755, Z = 7.856 },
    new Node { TrayName = "/C004", X = 11.174, Y = -24.127, Z = 7.85 }
};
            trays["/C004"] = c004Nodes;

            // Tray /C005 Nodes
            var c005Nodes = new List<Node>
{
    new Node { TrayName = "/C005", X = 11.174, Y = -24.127, Z = 7.85 },
    new Node { TrayName = "/C005", X = 11.161, Y = -36.033, Z = 7.856 }
};
            trays["/C005"] = c005Nodes;

            // Tray /C006 Nodes
            var c006Nodes = new List<Node>
{
    new Node { TrayName = "/C006", X = 11.104, Y = -40.494, Z = 5.446 },
    new Node { TrayName = "/C006", X = 11.02, Y = -41.065, Z = 5.446 },
    new Node { TrayName = "/C006", X = 0.535, Y = -41.153, Z = 5.446 }
};
            trays["/C006"] = c006Nodes;

            // Tray /C011 Nodes
            var c011Nodes = new List<Node>
{
    new Node { TrayName = "/C011", X = 11.161, Y = -36.033, Z = 7.856 },
    new Node { TrayName = "/C011", X = 11.161, Y = -40.305, Z = 7.87 }
};
            trays["/C011"] = c011Nodes;

            // Tray /C102 Nodes
            var c102Nodes = new List<Node>
{
    new Node { TrayName = "/C102", X = 26.607, Y = -20.365, Z = 5.956 },
    new Node { TrayName = "/C102", X = 26.553, Y = -19.018, Z = 6.043 },
    new Node { TrayName = "/C102", X = 26.554, Y = -15.125, Z = 7.556 },
    new Node { TrayName = "/C102", X = 26.593, Y = -14.323, Z = 7.556 },
    new Node { TrayName = "/C102", X = 26.583, Y = -14.29, Z = 6.188 }
};
            trays["/C102"] = c102Nodes;

            // Tray /C104 Nodes
            var c104Nodes = new List<Node>
{
    new Node { TrayName = "/C104", X = 17.794, Y = -20.145, Z = 5.956 },
    new Node { TrayName = "/C104", X = 26.051, Y = -20.128, Z = 5.956 }
};
            trays["/C104"] = c104Nodes;

            // Tray /C105 Nodes
            var c105Nodes = new List<Node>
{
    new Node { TrayName = "/C105", X = 9.528, Y = -20.133, Z = 7.856 },
    new Node { TrayName = "/C105", X = 10.54, Y = -20.149, Z = 7.206 },
    new Node { TrayName = "/C105", X = 15.635, Y = -20.131, Z = 7.177 },
    new Node { TrayName = "/C105", X = 16.928, Y = -20.133, Z = 5.956 },
    new Node { TrayName = "/C105", X = 17.794, Y = -20.145, Z = 5.956 }
};
            trays["/C105"] = c105Nodes;

            // Tray /C118 Nodes
            var c118Nodes = new List<Node>
{
    new Node { TrayName = "/C118", X = 26.675, Y = -20.908, Z = 5.925 },
    new Node { TrayName = "/C118", X = 26.675, Y = -20.908, Z = 5.525 },
    new Node { TrayName = "/C118", X = 26.675, Y = -20.908, Z = 5.525 }
};
            trays["/C118"] = c118Nodes;

            // Tray /C119 Nodes
            var c119Nodes = new List<Node>
{
    new Node { TrayName = "/C119", X = 26.675, Y = -23.908, Z = 5.525 },
    new Node { TrayName = "/C119", X = 26.675, Y = -26.746, Z = 5.525 },
    new Node { TrayName = "/C119", X = 26.675, Y = -27.375, Z = 4.896 },
    new Node { TrayName = "/C119", X = 26.675, Y = -28.995, Z = 4.896 },
    new Node { TrayName = "/C119", X = 25.319, Y = -28.995, Z = 4.896 },
    new Node { TrayName = "/C119", X = 25.319, Y = -29.598, Z = 4.896 }
};
            trays["/C119"] = c119Nodes;

            // Tray /C120 Nodes
            var c120Nodes = new List<Node>
{
    new Node { TrayName = "/C120", X = 25.319, Y = -29.598, Z = 4.896 },
    new Node { TrayName = "/C120", X = 25.319, Y = -31.259, Z = 4.896 },
    new Node { TrayName = "/C120", X = 25.319, Y = -32.673, Z = 4.896 }
};
            trays["/C120"] = c120Nodes;

            // Tray /C121 Nodes
            var c121Nodes = new List<Node>
{
    new Node { TrayName = "/C121", X = 25.319, Y = -32.673, Z = 4.896 },
    new Node { TrayName = "/C121", X = 25.319, Y = -38.051, Z = 4.896 },
    new Node { TrayName = "/C121", X = 24.641, Y = -38.051, Z = 4.896 }
};
            trays["/C121"] = c121Nodes;

            // Tray /C122 Nodes
            var c122Nodes = new List<Node>
{
    new Node { TrayName = "/C122", X = 24.641, Y = -38.051, Z = 4.896 },
    new Node { TrayName = "/C122", X = 21.491, Y = -38.051, Z = 4.896 }
};
            trays["/C122"] = c122Nodes;

            // Tray /C141 Nodes
            var c141Nodes = new List<Node>
{
    new Node { TrayName = "/C141", X = 10.951, Y = -40.164, Z = 7.774 },
    new Node { TrayName = "/C141", X = 10.893, Y = -40.143, Z = 5.531 },
    new Node { TrayName = "/C141", X = 7.3, Y = -40.036, Z = 5.446 },
    new Node { TrayName = "/C141", X = 7.182, Y = -37.654, Z = 5.446 }
};
            trays["/C141"] = c141Nodes;

            // Tray /DFB001 Nodes
            var dfb001Nodes = new List<Node>
{
    new Node { TrayName = "/DFB001", X = 31.627, Y = -9.055, Z = 1.511 },
    new Node { TrayName = "/DFB001", X = 33.33, Y = -9.065, Z = 1.511 },
    new Node { TrayName = "/DFB001", X = 32.72, Y = -9.087, Z = 1.511 }
};
            trays["/DFB001"] = dfb001Nodes;

            // Tray /DFB014 Nodes
            var dfb014Nodes = new List<Node>
{
    new Node { TrayName = "/DFB014", X = 28.922, Y = -9.012, Z = 1.511 },
    new Node { TrayName = "/DFB014", X = 31.627, Y = -9.055, Z = 1.511 }
};
            trays["/DFB014"] = dfb014Nodes;

            // Tray /DFB015 Nodes
            var dfb015Nodes = new List<Node>
{
    new Node { TrayName = "/DFB015", X = 28.725, Y = -12.459, Z = 1.511 },
    new Node { TrayName = "/DFB015", X = 28.693, Y = -10.635, Z = 1.511 },
    new Node { TrayName = "/DFB015", X = 28.975, Y = -9.557, Z = 1.511 }
};
            trays["/DFB015"] = dfb015Nodes;

            // Tray /DFC006 Nodes
            var dfc006Nodes = new List<Node>
{
    new Node { TrayName = "/DFC006", X = 28.063, Y = -12.083, Z = 1.211 },
    new Node { TrayName = "/DFC006", X = 33.158, Y = -12.017, Z = 1.211 },
    new Node { TrayName = "/DFC006", X = -34.077, Y = -12.009, Z = 1.211 }
};
            trays["/DFC006"] = dfc006Nodes;

            // Tray /DFC006-1 Nodes
            var dfc006_1Nodes = new List<Node>
{
    new Node { TrayName = "/DFC006-1", X = 33.129, Y = -11.653, Z = 1.211 },
    new Node { TrayName = "/DFC006-1", X = 33.087, Y = -10.439, Z = 1.211 }
};
            trays["/DFC006-1"] = dfc006_1Nodes;

            // Tray /DFC006-2 Nodes
            var dfc006_2Nodes = new List<Node>
{
    new Node { TrayName = "/DFC006-2", X = 34.005, Y = 11.652, Z = 1.211 },
    new Node { TrayName = "/DFC006-2", X = 33.99, Y = -11.045, Z = 1.211 }
};
            trays["/DFC006-2"] = dfc006_2Nodes;

            return trays;
        }   


        public static List<Node> GetBNodes()
        {
            var allSections = CoordinatesData();
            var bTrayes = new List<Node>();
            foreach (var section in allSections)
            {
                if (section.Key.StartsWith("/B"))
                {
                    bTrayes.AddRange(section.Value);
                }
            }
            return bTrayes;
        }

        public static List<Node> GetBCTrays()
        {
            var allSections = CoordinatesData();
            var bcTrayes = new List<Node>();
            foreach (var section in allSections)
            {
                if (section.Key.StartsWith("/BC"))
                {
                    bcTrayes.AddRange(section.Value);
                }
            }
            return bcTrayes;
        }

        public static List<Node> GetCTrays()
        {
            var allSections = CoordinatesData();
            var cTrayes = new List<Node>();
            foreach (var section in allSections)
            {
                if (section.Key.StartsWith("/C"))
                {
                    cTrayes.AddRange(section.Value);
                }
            }
            return cTrayes;
        }

        public static List<Node> GetDFBTrays()
        {
            var allSections = CoordinatesData();
            var dfTrayes = new List<Node>();
            foreach (var section in allSections)
            {
                if (section.Key.StartsWith("/DFB"))
                {
                    dfTrayes.AddRange(section.Value);
                }
            }
            return dfTrayes;
        }

        public static List<Node> GetDFCTrays()
        {
            var allSections = CoordinatesData();
            var dfTrayes = new List<Node>();
            foreach (var section in allSections)
            {
                if (section.Key.StartsWith("/DFC"))
                {
                    dfTrayes.AddRange(section.Value);
                }
            }
            return dfTrayes;
        }
    }
}