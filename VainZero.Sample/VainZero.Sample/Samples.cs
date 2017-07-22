using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace VainZero.Sample
{
    namespace OKCases
    {
        public class InitializeFieldViaConstructorSample
        {
            readonly int value;

            public InitializeFieldViaConstructorSample()
            {
                value = 1;

                Debug.WriteLine(value);
            }
        }

        public class InitializeFieldViaInitializerSample
        {
            readonly int value = 1;

            public InitializeFieldViaInitializerSample()
            {
                Debug.WriteLine(value);
            }
        }

        public class NotInitializePublicFieldSample
        {
            public int Value;

            public NotInitializePublicFieldSample()
            {
            }
        }

        public class OutArgumentSample
        {
            int value;

            public OutArgumentSample()
            {
                if (int.TryParse("1", out value))
                {
                    Debug.Write(value);
                }
            }
        }

        public class InitializePropertyViaConstructorSample
        {
            public int Value { get; set; }

            public InitializePropertyViaConstructorSample()
            {
                Value = 1;

                Debug.WriteLine(Value);
            }
        }

        public class InitializeReadOnlyPropertyViaConstructorSample
        {
            public int Value { get; }

            public InitializeReadOnlyPropertyViaConstructorSample()
            {
                Value = 1;

                Debug.WriteLine(Value);
            }
        }

        public class InitializePropertyViaInitializerSample
        {
            public int Value { get; set; } = 1;

            public InitializePropertyViaInitializerSample()
            {
                Debug.WriteLine(Value);
            }
        }

        public class InitializeReadOnlyPropertyViaInitializerSample
        {
            public int Value { get; } = 1;

            public InitializeReadOnlyPropertyViaInitializerSample()
            {
                Debug.WriteLine(Value);
            }
        }

        public class NotInitializePublicSetterPropertySample
        {
            public int Value { get; set; }

            public NotInitializePublicSetterPropertySample()
            {
            }
        }

        public class InitializeViaMethodSample
        {
            int value;

            void Initialize()
            {
                value = 1;
            }

            public InitializeViaMethodSample()
            {
                Initialize();
            }
        }

        public class InitializeViaSetterSample
        {
            int value;
            int Value
            {
                get { return value; }
                set { this.value = value; }
            }

            public InitializeViaSetterSample()
            {
                Value = 1;
            }
        }

        public class DelegateConstructorSample
        {
            int value1;
            int value2;

            DelegateConstructorSample(double x)
            {
                value1 = 1;
            }

            public DelegateConstructorSample(string x)
                : this(1)
            {
                value2 = 2;
            }
        }
    }

    namespace NGCases
    {
        public class NotInitializeFieldSample
        {
            int value;

            public NotInitializeFieldSample()
            {
                // NG: Use before initialization.
                Debug.WriteLine(value);

                value = 1;
            }
        }

        public class RefArgumentSample
        {
            int value;

            public RefArgumentSample()
            {
                // NG: `ref` arguments can be used before initialization.
                Interlocked.Increment(ref value);

                Debug.WriteLine(value);
            }
        }

        public class NotInitializePropertySample
        {
            public int Value { get; set; }

            public NotInitializePropertySample(int x)
            {
                // NG: Use before initialization.
                Debug.WriteLine(Value);

                Value = 1;
            }
        }

        public class NotInitializeReadOnlyPropertySample
        {
            public int Value { get; }

            public NotInitializeReadOnlyPropertySample()
            {
                // NG: Not initialized.
            }

            public NotInitializeReadOnlyPropertySample(int x)
            {
                // NG: Use before initialization.
                Debug.WriteLine(Value);

                Value = 1;
            }
        }

        public class NotInitializePrivateSetterPropertySample
        {
            public int Value { get; private set; }

            public NotInitializePrivateSetterPropertySample()
            {
                // NG: Not initialized.
            }
        }
    }
}
