using AndroidX.Media3.Common;

namespace Shiny.Maui.Controls.Media;

/// <summary>
/// Bridges the Media3 <c>Player.Listener</c> callbacks we care about onto plain delegates.
/// </summary>
/// <remarks>
/// Every member of <see cref="IPlayerListener"/> is a Java default method, but the binding does not
/// forward them to Media3's defaults (see the no-ops below), so all of them are implemented here; only
/// the first four do anything.
/// </remarks>
class AndroidPlayerListener : Java.Lang.Object, IPlayerListener
{
    readonly Action<int> onPlaybackStateChanged;
    readonly Action<bool> onIsPlayingChanged;
    readonly Action<PlaybackException?> onPlayerError;
    readonly Action onTracksReady;

    public AndroidPlayerListener(
        Action<int> onPlaybackStateChanged,
        Action<bool> onIsPlayingChanged,
        Action<PlaybackException?> onPlayerError,
        Action onTracksReady)
    {
        this.onPlaybackStateChanged = onPlaybackStateChanged;
        this.onIsPlayingChanged = onIsPlayingChanged;
        this.onPlayerError = onPlayerError;
        this.onTracksReady = onTracksReady;
    }

    public void OnPlaybackStateChanged(int playbackState) => this.onPlaybackStateChanged(playbackState);

    public void OnIsPlayingChanged(bool isPlaying) => this.onIsPlayingChanged(isPlaying);

    public void OnPlayerError(PlaybackException? error) => this.onPlayerError(error);

    public void OnVideoSizeChanged(VideoSize? videoSize) => this.onTracksReady();

    // ---- the rest: no-ops, but every one has to exist ----
    //
    // The binding's default-interface-method forwarding is broken for this listener: the Java callable
    // wrapper sends every callback to managed code, and one with no managed body throws
    // AbstractMethodError (onSurfaceSizeChanged, then onEvents, killed the app the moment a video
    // started). So each member is spelled out, even though none of them does anything.

    public void OnAudioAttributesChanged(AudioAttributes? audioAttributes) { }
    public void OnAudioSessionIdChanged(int audioSessionId) { }
    public void OnAvailableCommandsChanged(PlayerCommands? availableCommands) { }
    public void OnCues(AndroidX.Media3.Common.Text.CueGroup? cueGroup) { }
    public void OnCuesDeprecated(IList<AndroidX.Media3.Common.Text.Cue>? cues) { }
    public void OnDeviceInfoChanged(AndroidX.Media3.Common.DeviceInfo? deviceInfo) { }
    public void OnDeviceVolumeChanged(int volume, bool muted) { }
    public void OnEvents(IPlayer? player, PlayerEvents? events) { }
    public void OnIsLoadingChanged(bool isLoading) { }
    public void OnLoadingChanged(bool isLoading) { }
    public void OnMaxSeekToPreviousPositionChanged(long maxSeekToPreviousPositionMs) { }
    public void OnMediaItemTransition(MediaItem? mediaItem, int reason) { }
    public void OnMediaMetadataChanged(AndroidX.Media3.Common.MediaMetadata? mediaMetadata) { }
    public void OnMetadata(Metadata? metadata) { }
    public void OnPlaybackParametersChanged(PlaybackParameters? playbackParameters) { }
    public void OnPlaybackSuppressionReasonChanged(int playbackSuppressionReason) { }
    public void OnPlayerErrorChanged(PlaybackException? error) { }
    public void OnPlayerStateChanged(bool playWhenReady, int playbackState) { }
    public void OnPlaylistMetadataChanged(AndroidX.Media3.Common.MediaMetadata? mediaMetadata) { }
    public void OnPlayWhenReadyChanged(bool playWhenReady, int reason) { }
    public void OnPositionDiscontinuity(PlayerPositionInfo? oldPosition, PlayerPositionInfo? newPosition, int reason) { }
    public void OnRenderedFirstFrame() { }
    public void OnRepeatModeChanged(int repeatMode) { }
    public void OnSeekBackIncrementChanged(long seekBackIncrementMs) { }
    public void OnSeekForwardIncrementChanged(long seekForwardIncrementMs) { }
    public void OnShuffleModeEnabledChanged(bool shuffleModeEnabled) { }
    public void OnSkipSilenceEnabledChanged(bool skipSilenceEnabled) { }
    public void OnSurfaceSizeChanged(int width, int height) { }
    public void OnTimelineChanged(Timeline? timeline, int reason) { }
    public void OnTracksChanged(Tracks? tracks) { }
    public void OnTrackSelectionParametersChanged(TrackSelectionParameters? parameters) { }
    public void OnVolumeChanged(float volume) { }
}
