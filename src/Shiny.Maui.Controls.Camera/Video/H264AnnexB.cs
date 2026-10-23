namespace Shiny.Maui.Controls.Camera.Video;

/// <summary>
/// The H.264 byte-stream plumbing every platform shares: splitting an Annex B access unit into its NAL units,
/// telling a key frame from a delta, and converting to and from the length-prefixed (AVCC) form VideoToolbox
/// speaks.
/// </summary>
/// <remarks>
/// <para>
/// The wire form is Annex B, because it is self-delimiting and because Android's MediaCodec already speaks it:
/// each NAL unit follows a <c>00 00 00 01</c> (or three-byte <c>00 00 01</c>) start code. VideoToolbox wants
/// each NAL unit prefixed with its big-endian length instead, and keeps the SPS and PPS out of the frame, in
/// the format description. These helpers are the only place either conversion happens, which is what lets
/// them be tested without a codec.
/// </para>
/// <para>
/// No emulation-prevention handling is needed: a start code cannot occur inside a NAL unit (the encoder
/// inserts <c>03</c> bytes to guarantee it), and nothing here reads inside a payload beyond its header byte.
/// </para>
/// </remarks>
static class H264AnnexB
{
    /// <summary>NAL unit type of a non-IDR slice (a delta frame).</summary>
    public const int NonIdrSlice = 1;

    /// <summary>NAL unit type of an IDR slice (a key frame).</summary>
    public const int IdrSlice = 5;

    /// <summary>NAL unit type of supplemental enhancement information.</summary>
    public const int Sei = 6;

    /// <summary>NAL unit type of a sequence parameter set.</summary>
    public const int Sps = 7;

    /// <summary>NAL unit type of a picture parameter set.</summary>
    public const int Pps = 8;

    /// <summary>NAL unit type of an access unit delimiter.</summary>
    public const int AccessUnitDelimiter = 9;

    /// <summary>The four-byte start code written in front of every NAL unit this library emits.</summary>
    public static ReadOnlySpan<byte> StartCode => [0, 0, 0, 1];

    /// <summary>The <c>nal_unit_type</c> of a NAL unit, from its header byte.</summary>
    public static int NalType(byte header) => header & 0x1F;

    /// <summary>
    /// Split an Annex B stream into its NAL units. Each range covers the NAL unit itself (header byte first),
    /// with the start code and any trailing zero padding removed.
    /// </summary>
    /// <remarks>
    /// Accepts three- and four-byte start codes, mixed. Bytes ahead of the first start code are ignored, so a
    /// stream with leading garbage still parses; a stream with no start code at all yields nothing.
    /// </remarks>
    public static List<Range> Split(ReadOnlySpan<byte> annexB)
    {
        var units = new List<Range>();
        var start = -1;
        var i = 0;
        while (i + 2 < annexB.Length)
        {
            if (annexB[i] == 0 && annexB[i + 1] == 0 && annexB[i + 2] == 1)
            {
                if (start >= 0)
                    AddUnit(annexB, units, start, i);

                i += 3;
                start = i;
                continue;
            }
            i++;
        }

        if (start >= 0)
            AddUnit(annexB, units, start, annexB.Length);

        return units;
    }

    static void AddUnit(ReadOnlySpan<byte> data, List<Range> units, int start, int end)
    {
        // The zero byte of a four-byte start code (and any trailing_zero_8bits) belongs to no NAL unit: a NAL
        // unit always ends in its rbsp stop bit, so it never ends in 0x00.
        while (end > start && data[end - 1] == 0)
            end--;

        if (end > start)
            units.Add(new Range(start, end));
    }

    /// <summary>Whether the access unit contains an IDR slice, i.e. decodes without any earlier frame.</summary>
    public static bool IsKeyFrame(ReadOnlySpan<byte> annexB) => Contains(annexB, IdrSlice);

    /// <summary>Whether the access unit carries both an SPS and a PPS, so a decoder can start from it.</summary>
    public static bool HasParameterSets(ReadOnlySpan<byte> annexB) => Contains(annexB, Sps) && Contains(annexB, Pps);

    /// <summary>Whether any NAL unit in the stream has the given type.</summary>
    public static bool Contains(ReadOnlySpan<byte> annexB, int nalType)
    {
        foreach (var unit in Split(annexB))
        {
            if (NalType(annexB[unit][0]) == nalType)
                return true;
        }
        return false;
    }

    /// <summary>
    /// The first SPS and PPS in the stream (without start codes), or <c>null</c> for whichever is missing.
    /// </summary>
    public static (byte[]? Sps, byte[]? Pps) GetParameterSets(ReadOnlySpan<byte> annexB)
    {
        byte[]? sps = null, pps = null;
        foreach (var unit in Split(annexB))
        {
            var nal = annexB[unit];
            switch (NalType(nal[0]))
            {
                case Sps when sps is null:
                    sps = nal.ToArray();
                    break;
                case Pps when pps is null:
                    pps = nal.ToArray();
                    break;
            }
        }
        return (sps, pps);
    }

    /// <summary>
    /// Convert an Annex B access unit into the length-prefixed form a <c>CMSampleBuffer</c> carries: each NAL
    /// unit preceded by its 4-byte big-endian length. Parameter sets and access unit delimiters are left out,
    /// because VideoToolbox takes those from the format description, not the frame.
    /// </summary>
    /// <returns>The payload, or an empty array when the access unit holds nothing to decode.</returns>
    public static byte[] ToLengthPrefixed(ReadOnlySpan<byte> annexB)
    {
        var units = Split(annexB);
        var size = 0;
        foreach (var unit in units)
        {
            if (IsSliceData(annexB[unit][0]))
                size += 4 + unit.GetOffsetAndLength(annexB.Length).Length;
        }

        var result = new byte[size];
        var at = 0;
        foreach (var unit in units)
        {
            var nal = annexB[unit];
            if (!IsSliceData(nal[0]))
                continue;

            WriteBigEndian(result.AsSpan(at), nal.Length);
            nal.CopyTo(result.AsSpan(at + 4));
            at += 4 + nal.Length;
        }
        return result;
    }

    static bool IsSliceData(byte header) => NalType(header) is not (Sps or Pps or AccessUnitDelimiter);

    /// <summary>
    /// Convert a length-prefixed (AVCC) access unit, as a VideoToolbox encoder produces it, into Annex B, writing
    /// <paramref name="parameterSets"/> (each with its own start code) ahead of it. Pass the SPS and PPS for a key
    /// frame and nothing for a delta frame.
    /// </summary>
    /// <param name="lengthPrefixed">The sample buffer's data.</param>
    /// <param name="lengthSize">Bytes in each length prefix (VideoToolbox uses 4; 1, 2 and 4 are legal).</param>
    /// <param name="parameterSets">NAL units to write first, without start codes.</param>
    /// <exception cref="FormatException">A length runs past the end of the buffer.</exception>
    public static byte[] FromLengthPrefixed(ReadOnlySpan<byte> lengthPrefixed, int lengthSize, IReadOnlyList<byte[]>? parameterSets = null)
    {
        if (lengthSize is not (1 or 2 or 4))
            throw new ArgumentOutOfRangeException(nameof(lengthSize), lengthSize, "A NAL length prefix is 1, 2 or 4 bytes.");

        // First pass sizes the output, and validates, so the second can write without bounds surprises.
        var size = 0;
        if (parameterSets is not null)
        {
            foreach (var ps in parameterSets)
                size += 4 + ps.Length;
        }

        var at = 0;
        while (at + lengthSize <= lengthPrefixed.Length)
        {
            var length = ReadBigEndian(lengthPrefixed.Slice(at, lengthSize));
            at += lengthSize;
            if (length < 0 || length > lengthPrefixed.Length - at)
                throw new FormatException($"A NAL unit of {length} bytes runs past the end of a {lengthPrefixed.Length}-byte frame.");

            size += 4 + length;
            at += length;
        }

        var result = new byte[size];
        var write = 0;
        if (parameterSets is not null)
        {
            foreach (var ps in parameterSets)
            {
                StartCode.CopyTo(result.AsSpan(write));
                ps.CopyTo(result.AsSpan(write + 4));
                write += 4 + ps.Length;
            }
        }

        at = 0;
        while (at + lengthSize <= lengthPrefixed.Length)
        {
            var length = ReadBigEndian(lengthPrefixed.Slice(at, lengthSize));
            at += lengthSize;
            StartCode.CopyTo(result.AsSpan(write));
            lengthPrefixed.Slice(at, length).CopyTo(result.AsSpan(write + 4));
            write += 4 + length;
            at += length;
        }
        return result;
    }

    /// <summary>
    /// Put <paramref name="config"/> (the codec-config buffer MediaCodec emits once, holding the SPS and PPS in
    /// Annex B) in front of a key frame that arrived without it, so the frame can start a decoder on its own.
    /// A frame that already carries both parameter sets, or a null config, is returned as it is.
    /// </summary>
    public static byte[] EnsureParameterSets(byte[] keyFrame, byte[]? config)
    {
        if (config is null || config.Length == 0 || HasParameterSets(keyFrame))
            return keyFrame;

        var result = new byte[config.Length + keyFrame.Length];
        config.CopyTo(result, 0);
        keyFrame.CopyTo(result, config.Length);
        return result;
    }

    static int ReadBigEndian(ReadOnlySpan<byte> bytes)
    {
        var value = 0;
        foreach (var b in bytes)
            value = (value << 8) | b;
        return value;
    }

    static void WriteBigEndian(Span<byte> destination, int value)
    {
        destination[0] = (byte)(value >> 24);
        destination[1] = (byte)(value >> 16);
        destination[2] = (byte)(value >> 8);
        destination[3] = (byte)value;
    }
}
