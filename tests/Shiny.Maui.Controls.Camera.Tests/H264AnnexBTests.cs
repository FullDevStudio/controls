using Shiny.Controls.Camera;
using Shiny.Maui.Controls.Camera.Video;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Camera.Tests;

public class H264AnnexBTests
{
    // header bytes: forbidden_zero(0) | nal_ref_idc(3) | type
    static readonly byte[] Sps = [0x67, 0x42, 0xC0, 0x1E, 0xDA];
    static readonly byte[] Pps = [0x68, 0xCE, 0x3C, 0x80];
    static readonly byte[] Idr = [0x65, 0x88, 0x84, 0x00, 0x21];
    static readonly byte[] Delta = [0x41, 0x9A, 0x02, 0x07];
    static readonly byte[] Aud = [0x09, 0xF0];

    static byte[] AnnexB(params byte[][] nals)
    {
        var result = new List<byte>();
        foreach (var nal in nals)
        {
            result.AddRange([0, 0, 0, 1]);
            result.AddRange(nal);
        }
        return result.ToArray();
    }

    static byte[][] Units(byte[] stream) =>
        H264AnnexB.Split(stream).Select(r => stream[r]).ToArray();

    [Fact]
    public void Split_returns_each_nal_without_its_start_code()
    {
        var units = Units(AnnexB(Sps, Pps, Idr));

        units.Length.ShouldBe(3);
        units[0].ShouldBe(Sps);
        units[1].ShouldBe(Pps);
        units[2].ShouldBe(Idr);
    }

    [Fact]
    public void Split_accepts_three_byte_start_codes_mixed_with_four()
    {
        byte[] stream = [0, 0, 1, .. Sps, 0, 0, 0, 1, .. Pps, 0, 0, 1, .. Delta];

        var units = Units(stream);

        units.Length.ShouldBe(3);
        units[0].ShouldBe(Sps);
        units[1].ShouldBe(Pps);
        units[2].ShouldBe(Delta);
    }

    [Fact]
    public void Split_keeps_a_zero_inside_a_nal_but_drops_trailing_padding()
    {
        // Idr carries a 00 in its middle; the stream ends in trailing_zero_8bits
        byte[] stream = [.. AnnexB(Idr), 0, 0];

        var units = Units(stream);

        units.Length.ShouldBe(1);
        units[0].ShouldBe(Idr);
    }

    [Fact]
    public void Split_ignores_bytes_ahead_of_the_first_start_code()
    {
        byte[] stream = [0xFF, 0x12, .. AnnexB(Delta)];

        Units(stream).ShouldHaveSingleItem().ShouldBe(Delta);
    }

    [Fact]
    public void Split_finds_nothing_without_a_start_code()
        => H264AnnexB.Split(Delta).ShouldBeEmpty();

    [Fact]
    public void Nal_types_are_read_from_the_header_byte()
    {
        H264AnnexB.NalType(Sps[0]).ShouldBe(H264AnnexB.Sps);
        H264AnnexB.NalType(Pps[0]).ShouldBe(H264AnnexB.Pps);
        H264AnnexB.NalType(Idr[0]).ShouldBe(H264AnnexB.IdrSlice);
        H264AnnexB.NalType(Delta[0]).ShouldBe(H264AnnexB.NonIdrSlice);
    }

    [Fact]
    public void A_key_frame_is_one_with_an_idr_slice()
    {
        H264AnnexB.IsKeyFrame(AnnexB(Sps, Pps, Idr)).ShouldBeTrue();
        H264AnnexB.IsKeyFrame(AnnexB(Delta)).ShouldBeFalse();
    }

    [Fact]
    public void Parameter_sets_need_both_sps_and_pps()
    {
        H264AnnexB.HasParameterSets(AnnexB(Sps, Pps, Idr)).ShouldBeTrue();
        H264AnnexB.HasParameterSets(AnnexB(Sps, Idr)).ShouldBeFalse();
        H264AnnexB.HasParameterSets(AnnexB(Idr)).ShouldBeFalse();
    }

    [Fact]
    public void GetParameterSets_returns_the_first_of_each()
    {
        var (sps, pps) = H264AnnexB.GetParameterSets(AnnexB(Aud, Sps, Pps, Idr));
        sps.ShouldBe(Sps);
        pps.ShouldBe(Pps);

        var (none, alsoNone) = H264AnnexB.GetParameterSets(AnnexB(Delta));
        none.ShouldBeNull();
        alsoNone.ShouldBeNull();
    }

    [Fact]
    public void ToLengthPrefixed_drops_parameter_sets_and_delimiters_and_prefixes_lengths()
    {
        var avcc = H264AnnexB.ToLengthPrefixed(AnnexB(Aud, Sps, Pps, Idr));

        avcc.ShouldBe([0, 0, 0, (byte)Idr.Length, .. Idr]);
    }

    [Fact]
    public void ToLengthPrefixed_of_nothing_to_decode_is_empty()
        => H264AnnexB.ToLengthPrefixed(AnnexB(Sps, Pps)).ShouldBeEmpty();

    [Fact]
    public void FromLengthPrefixed_writes_parameter_sets_first()
    {
        byte[] avcc = [0, 0, 0, (byte)Idr.Length, .. Idr];

        var annexB = H264AnnexB.FromLengthPrefixed(avcc, 4, [Sps, Pps]);

        annexB.ShouldBe(AnnexB(Sps, Pps, Idr));
    }

    [Fact]
    public void FromLengthPrefixed_converts_every_nal_of_a_delta_frame()
    {
        byte[] avcc = [0, 0, 0, (byte)Delta.Length, .. Delta, 0, 0, 0, (byte)Delta.Length, .. Delta];

        H264AnnexB.FromLengthPrefixed(avcc, 4).ShouldBe(AnnexB(Delta, Delta));
    }

    [Fact]
    public void FromLengthPrefixed_honours_short_length_prefixes()
    {
        byte[] avcc = [0, (byte)Delta.Length, .. Delta];

        H264AnnexB.FromLengthPrefixed(avcc, 2).ShouldBe(AnnexB(Delta));
    }

    [Fact]
    public void FromLengthPrefixed_rejects_a_length_past_the_end()
    {
        byte[] avcc = [0, 0, 0, 99, .. Delta];

        Should.Throw<FormatException>(() => H264AnnexB.FromLengthPrefixed(avcc, 4));
    }

    [Fact]
    public void Round_trip_from_the_encoder_to_the_decoder_keeps_the_slices()
    {
        // what VideoToolbox emits for a key frame -> the wire -> what VideoToolbox decodes
        byte[] encoded = [0, 0, 0, (byte)Idr.Length, .. Idr];
        var wire = H264AnnexB.FromLengthPrefixed(encoded, 4, [Sps, Pps]);

        H264AnnexB.IsKeyFrame(wire).ShouldBeTrue();
        H264AnnexB.HasParameterSets(wire).ShouldBeTrue();
        H264AnnexB.ToLengthPrefixed(wire).ShouldBe(encoded);
    }

    [Fact]
    public void EnsureParameterSets_prepends_the_codec_config_to_a_bare_key_frame()
    {
        var config = AnnexB(Sps, Pps);
        var bare = AnnexB(Idr);

        H264AnnexB.EnsureParameterSets(bare, config).ShouldBe(AnnexB(Sps, Pps, Idr));
    }

    [Fact]
    public void EnsureParameterSets_leaves_a_complete_key_frame_alone()
    {
        var complete = AnnexB(Sps, Pps, Idr);

        H264AnnexB.EnsureParameterSets(complete, AnnexB(Sps, Pps)).ShouldBeSameAs(complete);
        H264AnnexB.EnsureParameterSets(AnnexB(Idr), null).ShouldBe(AnnexB(Idr));
    }
}
