namespace NobleMuffins.LimbHacker.Guts
{
    struct SplitAction
    {
        public const int nullIndex = -1;

        public const int TO_ALFA = 0x01, TO_BRAVO = 0x02, INTERSECT = 0x04;

        public int flags;          //1
        public int cloneOf;
        public int index0, index1, realIndex;  //4 * 4 = 16 bytes
        public float intersectionResult;                            //4 bytes

        public SplitAction(bool _toFront, bool _toBack, int _index0)
        {
            flags = 0;
            if (_toFront) flags = flags | TO_ALFA;
            if (_toBack) flags = flags | TO_BRAVO;
            index0 = _index0;
            index1 = nullIndex;
            cloneOf = nullIndex;
            realIndex = index0;
            intersectionResult = 0f;
        }

        public SplitAction(int _index0, int _index1)
        {
            flags = TO_ALFA | TO_BRAVO | INTERSECT;
            index0 = _index0;
            index1 = _index1;
            cloneOf = nullIndex;
            realIndex = nullIndex;
            intersectionResult = 0f;
        }

        public new string ToString()
        {
            return string.Format("SplitAction: Geometry={1}", realIndex);
        }
    };
}