using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JC2.Save
{
    public class ChaosPointInfo
    {
        public static Dictionary<string, int> points;
        static ChaosPointInfo()
        {
            points = new Dictionary<string, int>();

            // Fuel storage/transport/production
            points.Add("Fuel Depot", 150);
            points.Add("Fuel Silo", 500);
            points.Add("Fuel Shaft", 250);
            points.Add("Gas Holder", 500);
            points.Add("Gas Pump", 100);
            points.Add("Gas Tank", 10);
            points.Add("Red Barrel", 10);
            points.Add("Pipeline", 1000);
            points.Add("Offshore Rig", 1500);

            // Communication equipment
            points.Add("Radar", 250);
            points.Add("Radio Mast", 1000);
            points.Add("Broadcast Tower", 250);
            points.Add("Satellite Dish", 250);
            points.Add("Mobile Radar", 500);
            points.Add("Communication Station", 1500);

            // Electric power
            points.Add("Wind Turbine", 500);
            points.Add("Generator", 250);
            points.Add("Transformer", 250);

            // Regime influence
            points.Add("Baby Panay Statue", 1000);
            points.Add("Propaganda Trailer", 1000);

            // Others
            points.Add("Factory Chimney", 500);
            points.Add("Water Tower", 250);
            points.Add("Crane", 500);
            points.Add("Colonel", 1000);
            points.Add("SAM Site", 500);

        }
    }
}
