using System;
using System.Collections.Generic;
using System.Text;

namespace MotionDetection.Configuration
{
    public class OnOffActionDescriptor
    {
        public IEnumerable<string> On { get; set; }

        public IEnumerable<string> Off { get; set; }
    }
}
