using System.Globalization;

namespace MSP.Scripts.Session
{
    public class RegionCoords
    {
        public RegionCoords()
        {
        }
        public RegionCoords(float a_bottomLeftX, float a_bottomLeftY, float a_topRightX, float a_topRightY)
        {
            BottomLeftX = a_bottomLeftX;
            BottomLeftY = a_bottomLeftY;
            TopRightX = a_topRightX;
            TopRightY = a_topRightY;
        }

        [Newtonsoft.Json.JsonProperty("region_bottom_left_x")]
        public float BottomLeftX { get; set; }

        [Newtonsoft.Json.JsonProperty("region_bottom_left_y")]
        public float BottomLeftY { get; set; }

        [Newtonsoft.Json.JsonProperty("region_top_right_x")]
        public float TopRightX { get; set; }

        [Newtonsoft.Json.JsonProperty("region_top_right_y")]
        public float TopRightY { get; set; }

        public override string ToString()
        {
            return $"BottomLeftX: {BottomLeftX.ToString("G9", CultureInfo.InvariantCulture)}, BottomLeftY: "+
                   $"{BottomLeftY.ToString("G9", CultureInfo.InvariantCulture)}, TopRightX: "+
                   $"{TopRightX.ToString("G9", CultureInfo.InvariantCulture)}, TopRightY: "+
                   $"{TopRightY.ToString("G9", CultureInfo.InvariantCulture)}";
        }
    }
}