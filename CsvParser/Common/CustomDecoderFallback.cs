using System;
using System.Collections.Generic;
using System.Text;

namespace CsvParser.Common
{
    public class CustomDecoderFallback : DecoderFallback
    {
        private byte[] _bytesUnknown;
        private readonly int _bufferLength;
        private readonly DecoderFallback _parent;

        public override int MaxCharCount => _parent.MaxCharCount;
        public CustomDecoderFallback(int buffer_length, DecoderFallback parent_decoder_fallback)
        {
            _bufferLength = buffer_length;
            _parent = parent_decoder_fallback;
        }

        public override DecoderFallbackBuffer CreateFallbackBuffer()
        {
            _bytesUnknown = null;
            return new CustomDecoderFallbackBuffer(this);
        }

        public byte[] ResetBytesUnknown() { var rval = _bytesUnknown; _bytesUnknown = null; return rval; }


        private class CustomDecoderFallbackBuffer : DecoderFallbackBuffer
        {
            private readonly CustomDecoderFallback _parent;

            private DecoderFallbackBuffer _parentBuffer;
            public override int Remaining => _parentBuffer == null ? 0 : _parentBuffer.Remaining;

            public CustomDecoderFallbackBuffer(CustomDecoderFallback parent)
            {
                _parent = parent;
            }

            public override bool Fallback(byte[] bytesUnknown, int index)
            {
                if (bytesUnknown.Length + index == _parent._bufferLength)
                {
                    _parentBuffer = null;
                    _parent._bytesUnknown = bytesUnknown;
                    return false;
                }

                if (_parentBuffer == null)
                    _parentBuffer = _parent._parent.CreateFallbackBuffer();

                return _parentBuffer.Fallback(bytesUnknown, index);
            }

            public override char GetNextChar()
            {
                return _parentBuffer == null ? (char)0 : _parentBuffer.GetNextChar();
            }

            public override bool MovePrevious()
            {
                return _parentBuffer == null ? false : _parentBuffer.MovePrevious();
            }
        }

    }
}
