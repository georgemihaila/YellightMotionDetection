using System;
using System.Collections.Generic;
using System.Text;

namespace MotionDetection.Configuration
{
    public class CameraConfiguration
    {
        public int Cooloff { get; set; }

        public IEnumerable<CameraBinding> CameraBindings { get; set; }
    }
}
