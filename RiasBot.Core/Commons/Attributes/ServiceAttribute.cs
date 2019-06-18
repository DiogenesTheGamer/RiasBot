using System;

namespace RiasBot.Commons.Attributes
{
    //this is an attribute class implemented by all services
    public class ServiceAttribute : Attribute
    {
        public Type Implementation { get; }
        
        public ServiceAttribute()
        {
            
        }

        public ServiceAttribute(Type implementation)
        {
            Implementation = implementation;
        }
    }
}