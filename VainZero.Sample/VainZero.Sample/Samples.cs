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

        public class InitializeViaDelegatedConstructorSample
        {
            int value1;
            int value2;

            InitializeViaDelegatedConstructorSample(double x)
            {
                value1 = 1;

                // OK: This constructor doesn't initialize `value2`, however,
                //     the delegating constructor does.
            }

            public InitializeViaDelegatedConstructorSample(string x)
                : this(1)
            {
                // OK: `value1` is initialized by the delegated constructor.
                Debug.WriteLine(value1);

                value2 = 2;
            }
        }

        public class SetViaPublicSetterSample
        {
            int value;
            public int Value
            {
                get { return value; }
                set { this.value = value; }
            }

            public SetViaPublicSetterSample()
            {
                // OK: Because `value` is a backing field of `Value`, it doesn't need to be initialized.
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

        public class UseBeforeInitializationInGetterSample
        {
            int value;

            public int Value
            {
                get { return value; }
            }

            public UseBeforeInitializationInGetterSample()
            {
                Debug.WriteLine(Value);

                value = 1;
            }
        }

        public class UseBeforeInitializationInSetterSample
        {
            int value;

            public int Value
            {
                set
                {
                    Debug.WriteLine("Before: value = {0}", this.value);
                    this.value = value;
                    Debug.WriteLine("After: value = {0}", this.value);
                }
            }

            public UseBeforeInitializationInSetterSample()
            {
                Value = 1;
            }
        }

        public class UseBeforeInitializationInMethodSample
        {
            int value;

            public void Write()
            {
                Debug.WriteLine(value);
            }

            public UseBeforeInitializationInMethodSample()
            {
                Write();

                value = 1;
            }
        }

        public class SetViaPrivateSetterSample
        {
            int value;
            int Value
            {
                get { return value; }
                set { this.value = value; }
            }

            public SetViaPrivateSetterSample()
            {
            }
        }
    }
}
