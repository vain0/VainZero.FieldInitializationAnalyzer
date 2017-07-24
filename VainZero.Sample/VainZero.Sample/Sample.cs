using System;
using System.Collections.Generic;
using System.Text;

namespace VainZero.Sample
{
    public class InitializeViaIndexerSetterSample
    {
        static readonly int[] array = new[] { 1, 2, 3 };

        int offset;

        int this[int index]
        {
            get { return array[offset + index]; }
            set { array[offset + index] = value; }
        }


        public InitializeViaIndexerSetterSample()
        {
            this[0] = 1;
        }
    }
}
