using Microsoft.Maui;
using Shiny.Maui.Controls.Camera.Video;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Camera.Tests;

public class FrameRateGateTests
{
    const long Second = 1_000_000; // ticks per second for these tests

    static int CountPasses(FrameRateGate gate, double cameraFps, double seconds, double jitter = 0)
    {
        var passes = 0;
        var period = Second / cameraFps;
        var random = new Random(42);
        for (var i = 0; i < cameraFps * seconds; i++)
        {
            var t = (long)(i * period + (random.NextDouble() * 2 - 1) * jitter * period);
            if (gate.ShouldPass(t))
                passes++;
        }
        return passes;
    }

    [Fact]
    public void Halves_a_30fps_camera_to_15fps()
        => CountPasses(new FrameRateGate(15, Second), 30, 10).ShouldBeInRange(149, 151);

    [Fact]
    public void Holds_the_rate_when_frames_jitter()
    {
        // a strict "one interval since the last frame" gate lands near 10 fps here
        CountPasses(new FrameRateGate(15, Second), 30, 10, jitter: 0.1).ShouldBeInRange(145, 155);
    }

    [Fact]
    public void Runs_the_ble_profile_at_3fps()
        => CountPasses(new FrameRateGate(3, Second), 30, 10).ShouldBeInRange(29, 31);

    [Fact]
    public void Zero_rate_passes_every_frame()
        => CountPasses(new FrameRateGate(0, Second), 30, 1).ShouldBe(30);

    [Fact]
    public void The_first_frame_always_passes()
        => new FrameRateGate(1, Second).ShouldPass(123).ShouldBeTrue();

    [Fact]
    public void A_stall_does_not_release_a_burst()
    {
        var gate = new FrameRateGate(10, Second);
        gate.ShouldPass(0).ShouldBeTrue();

        // nothing for five seconds, then a 30 fps camera resumes: 10 fps from there, not a backlog of 50 slots
        var passes = 0;
        for (var i = 0; i < 30; i++)
        {
            if (gate.ShouldPass(5 * Second + i * Second / 30))
                passes++;
        }
        passes.ShouldBeInRange(9, 11);
    }

    [Fact]
    public void A_rate_change_takes_effect_from_the_next_frame()
    {
        var gate = new FrameRateGate(15, Second);
        CountPasses(gate, 30, 1).ShouldBeInRange(14, 16);

        gate.SetRate(3);
        var passes = 0;
        for (var i = 30; i < 60; i++)
        {
            if (gate.ShouldPass(i * Second / 30))
                passes++;
        }
        passes.ShouldBeInRange(2, 4);
    }

    [Fact]
    public void Reset_lets_the_next_frame_through()
    {
        var gate = new FrameRateGate(1, Second);
        gate.ShouldPass(0).ShouldBeTrue();
        gate.ShouldPass(Second / 10).ShouldBeFalse();

        gate.Reset();
        gate.ShouldPass(Second / 5).ShouldBeTrue();
    }
}


public class VideoScalingTests
{
    [Theory]
    [InlineData(1920, 1080, 640, 640, 360)]   // Wi-Fi profile, landscape
    [InlineData(1080, 1920, 640, 360, 640)]   // portrait keeps the long edge
    [InlineData(640, 480, 160, 160, 120)]     // BLE profile from CameraX's default analysis size
    [InlineData(1280, 720, 160, 160, 90)]
    [InlineData(320, 240, 640, 320, 240)]     // never scales up
    [InlineData(641, 481, 0, 640, 480)]       // no limit, still even
    [InlineData(1000, 3, 640, 640, 2)]        // a sliver keeps at least one step
    public void Fit_scales_the_long_edge_and_rounds_down_to_even(int w, int h, int max, int ew, int eh)
        => VideoScaling.Fit(w, h, max).ShouldBe((ew, eh));

    [Fact]
    public void Fit_aligns_to_a_coarser_step_when_asked()
        => VideoScaling.Fit(1280, 720, 640, alignment: 16).ShouldBe((640, 352));

    [Fact]
    public void Fit_of_an_empty_frame_is_empty()
        => VideoScaling.Fit(0, 480, 640).ShouldBe((0, 0));

    [Fact]
    public void Layout_fits_a_landscape_frame_into_a_portrait_view()
    {
        var (sx, sy, rotation) = VideoScaling.Layout(640, 360, 0, 360, 640, Aspect.AspectFit);

        sx.ShouldBe(360d / 640, 1e-9);
        sy.ShouldBe(sx);
        rotation.ShouldBe(0);
    }

    [Fact]
    public void Layout_measures_against_the_upright_size_after_a_quarter_turn()
    {
        // a 640x360 sensor frame rotated 90 is 360x640 upright, which fills a 360x640 view exactly
        var (sx, sy, rotation) = VideoScaling.Layout(640, 360, 90, 360, 640, Aspect.AspectFit);

        sx.ShouldBe(1d, 1e-9);
        sy.ShouldBe(1d, 1e-9);
        rotation.ShouldBe(90);
    }

    [Fact]
    public void Layout_fill_covers_and_fit_letterboxes()
    {
        var fit = VideoScaling.Layout(640, 480, 0, 1000, 500, Aspect.AspectFit);
        var fill = VideoScaling.Layout(640, 480, 0, 1000, 500, Aspect.AspectFill);

        fit.ScaleX.ShouldBe(500d / 480, 1e-9);   // height-bound
        fill.ScaleX.ShouldBe(1000d / 640, 1e-9); // width-bound, crops the height
    }

    [Fact]
    public void Layout_stretch_scales_each_axis()
    {
        var (sx, sy, _) = VideoScaling.Layout(640, 480, 270, 300, 900, Aspect.Fill);

        sx.ShouldBe(300d / 480, 1e-9);
        sy.ShouldBe(900d / 640, 1e-9);
    }

    [Theory]
    [InlineData(-90, 270)]
    [InlineData(450, 90)]
    [InlineData(45, 0)]
    [InlineData(180, 180)]
    public void Rotation_snaps_to_right_angles(int degrees, int expected)
        => VideoScaling.NormalizeRotation(degrees).ShouldBe(expected);
}


public unsafe class YuvScalerTests
{
    [Fact]
    public void Downscaling_averages_each_box()
    {
        byte[] source =
        [
            0, 100, 10, 10,
            100, 0, 10, 10,
            200, 200, 50, 51,
            200, 200, 49, 50
        ];
        var destination = new byte[4];

        fixed (byte* s = source)
        fixed (byte* d = destination)
            YuvScaler.ScalePlane(new YuvPlane(s, 4, 1), 4, 4, new YuvPlane(d, 2, 1), 2, 2);

        destination.ShouldBe([50, 10, 200, 50]);
    }

    [Fact]
    public void Same_size_copies_through_strides()
    {
        // a padded source row (stride 4 for width 3) into a tight destination
        byte[] source = [1, 2, 3, 99, 4, 5, 6, 99];
        var destination = new byte[6];

        fixed (byte* s = source)
        fixed (byte* d = destination)
            YuvScaler.ScalePlane(new YuvPlane(s, 4, 1), 3, 2, new YuvPlane(d, 3, 1), 3, 2);

        destination.ShouldBe([1, 2, 3, 4, 5, 6]);
    }

    [Fact]
    public void Interleaved_planes_write_only_their_own_samples()
    {
        // NV12-style chroma: U at even offsets, V at odd, pixel stride 2
        byte[] u = [10, 20];
        byte[] v = [30, 40];
        var interleaved = new byte[4];

        fixed (byte* pu = u)
        fixed (byte* pv = v)
        fixed (byte* d = interleaved)
        {
            YuvScaler.ScalePlane(new YuvPlane(pu, 2, 1), 2, 1, new YuvPlane(d, 4, 2), 2, 1);
            YuvScaler.ScalePlane(new YuvPlane(pv, 2, 1), 2, 1, new YuvPlane(d + 1, 4, 2), 2, 1);
        }

        interleaved.ShouldBe([10, 30, 20, 40]);
    }

    [Fact]
    public void Reads_an_interleaved_source()
    {
        // pick the U samples out of an interleaved UV row
        byte[] uv = [10, 30, 20, 40];
        var u = new byte[2];

        fixed (byte* s = uv)
        fixed (byte* d = u)
            YuvScaler.ScalePlane(new YuvPlane(s, 4, 2), 2, 1, new YuvPlane(d, 2, 1), 2, 1);

        u.ShouldBe([10, 20]);
    }
}
