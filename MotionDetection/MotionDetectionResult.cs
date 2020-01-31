using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Text;

namespace MotionDetection
{
    public class MotionDetectionResult
    {

        public (MovementDirection X, MovementDirection Y) Direction { get; set; }

        public double DetectionTimeMilliseconds { get; set; }
    }

    public enum MovementDirection { Up, Down, Left, Right, None }
}
