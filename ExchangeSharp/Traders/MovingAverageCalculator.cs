/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    /// <summary>
    /// Calculates a moving average value over a specified window
    /// </summary>
    public sealed class MovingAverageCalculator
    {
        private int _windowSize;
        private double[] _values;
        private int _nextValueIndex;
        private double _sum;
        private int _valuesIn;

        private double _weightingMultiplier;
        private double _previousMovingAverage;
        private double _previousExponentialMovingAverage;

        public double MovingAverage { get; private set; }
        public double Slope { get; private set; }
        public double ExponentialMovingAverage { get; private set; }
        public double ExponentialSlope { get; private set; }

        public override string ToString()
        {
            return string.Format("{0}:{1}, {2}:{3}", MovingAverage, Slope, ExponentialMovingAverage, ExponentialSlope);
        }

        /// <summary>
        /// Constructor - must call Reset before use
        /// </summary>
        public MovingAverageCalculator() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="windowSize"></param>
        public MovingAverageCalculator(int windowSize)
        {
            Reset(windowSize);
        }

        /// <summary>
        /// Updates the moving average with its next value.
        /// When IsMature is true and NextValue is called, a previous value will 'fall out' of the
        /// moving average.
        /// </summary>
        /// <param name="nextValue">The next value to be considered within the moving average.</param>
        public void NextValue(double nextValue)
        {
            // add new value to the sum
            _sum += nextValue;

            if (_valuesIn < _windowSize)
            {
                // we haven't yet filled our window
                _valuesIn++;
            }
            else
            {
                // remove oldest value from sum
                _sum -= _values[_nextValueIndex];
            }

            // store the value
            _values[_nextValueIndex] = nextValue;

            // progress the next value index pointer
            _nextValueIndex++;
            if (_nextValueIndex == _windowSize)
            {
                _nextValueIndex = 0;
            }
            MovingAverage = _sum / _valuesIn;
            Slope = MovingAverage - _previousMovingAverage;
            _previousMovingAverage = MovingAverage;

            // exponential moving average
            if (_previousExponentialMovingAverage != double.MinValue)
            {
                ExponentialMovingAverage = ((nextValue - _previousExponentialMovingAverage) * _weightingMultiplier) + _previousExponentialMovingAverage;
                ExponentialSlope = ExponentialMovingAverage - _previousExponentialMovingAverage;

                //update previous average
                _previousExponentialMovingAverage = ExponentialMovingAverage;
            }
            else
            {
                ExponentialMovingAverage = nextValue;
                ExponentialSlope = 0.0f;
                _previousExponentialMovingAverage = ExponentialMovingAverage;
            }
        }

        /// <summary>
        /// Gets a value indicating whether enough values have been provided to fill the
        /// specified window size.  Values returned from NextValue may still be used prior
        /// to IsMature returning true, however such values are not subject to the intended
        /// smoothing effect of the moving average's window size.
        /// </summary>
        public bool IsMature
        {
            get { return _valuesIn == _windowSize; }
        }

        /// <summary>
        /// Clears any accumulated state and resets the calculator to its initial configuration.
        /// Calling this method is the equivalent of creating a new instance.
        /// Must be called before first use
        /// </summary>
        public void Reset(int windowSize)
        {
            _windowSize = windowSize;
            _values = new double[_windowSize];
            _weightingMultiplier = 2.0f / (_values.Length + 1);
            _nextValueIndex = 0;
            _sum = 0;
            _valuesIn = 0;
            _previousExponentialMovingAverage = double.MinValue;
        }
    }
}
