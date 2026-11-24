using Verse;

namespace MudAndPlasteelFloor
{
    // Marks one of our terrains so the movement patch can recognise it in O(1) without string tag scans.
    // isMud       -> the slow-enemies mud floor.
    // isFastFloor -> the speed-boost plasteel floor (also carries the vanilla "FineFloor" tag in XML).
    public class MPFFloorExtension : DefModExtension
    {
        public bool isMud = false;
        public bool isFastFloor = false;
    }
}
