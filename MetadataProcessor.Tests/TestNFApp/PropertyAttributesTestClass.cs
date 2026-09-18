//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

namespace TestNFApp
{
    public class PropertyAttributesTestClass
    {
        private int _setOnlyBackingField;
        private int _setOnlyWithSetterAttributeBackingField;

        [DummyCustomAttribute1]
        [DummyCustomAttribute2]
        public int GetOnlyProperty { get; }

        [DummyCustomAttribute1]
        [DummyCustomAttribute2]
        public int SetOnlyProperty
        {
            set
            {
                _setOnlyBackingField = value;
            }
        }

        [DummyCustomAttribute1]
        [DummyCustomAttribute2]
        public int PropertyWithSetterAttribute
        {
            get;

            [DummyCustomAttribute2]
            set;
        }

        [DummyCustomAttribute1]
        public int PropertyWithOtherAttributeOnSetter
        {
            get;

            [DummyCustomAttribute2]
            set;
        }

        public int SetterOnlyAttribute
        {
            get;

            [DummyCustomAttribute1]
            set;
        }

        public int GetterOnlyAttribute
        {
            [DummyCustomAttribute1]
            get;

            set;
        }

        [DummyCustomAttribute1]
        [DummyCustomAttribute2]
        public int SetOnlyPropertyWithSetterAttribute
        {
            [DummyCustomAttribute1]
            set
            {
                _setOnlyWithSetterAttributeBackingField = value;
            }
        }

        public PropertyAttributesTestClass()
        {
            GetOnlyProperty = _setOnlyBackingField + _setOnlyWithSetterAttributeBackingField;
        }
    }
}
