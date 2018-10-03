using System;

namespace SQLIO2
{
    [AttributeUsage(AttributeTargets.Property)]
    class OptionAttribute : Attribute
    {
        public string Template { get; set; }

        public OptionAttribute(string template)
        {
            Template = template;
        }
    }
}
