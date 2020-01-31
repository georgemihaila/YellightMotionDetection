using System;
using System.Collections.Generic;
using System.Text;

namespace MotionDetection.Configuration
{
    public class CameraDirectionLightStateBinding
    {
        public MovementDirection Direction { get; set; }

        public OnOffActionDescriptor Actions { get; set; }
    }
}
