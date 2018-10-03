using System;

namespace SQLIO2
{
    [AttributeUsage(AttributeTargets.Property)]
    class ArgumentAttribute : Attribute
    {
        public int Order { get; set; }

        public ArgumentAttribute(int order)
        {
            Order = order;
        }
    }
}
