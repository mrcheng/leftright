mergeInto(LibraryManager.library, {
  LeftRight_MoveInput: function () {
    return window.leftRightInput ? window.leftRightInput.axis : 0;
  },
  LeftRight_JumpPressed: function () {
    var input = window.leftRightInput;
    if (!input) return 0;
    var pressed = input.jump; input.jump = false;
    return pressed ? 1 : 0;
  },
  LeftRight_UpdateLobby__deps: ['$WEBAudio'],
  LeftRight_UpdateLobby: function (code, status, humans, host, playing, x, jumping, menu, over, beat, travel, receiver) {
    if (typeof window === 'undefined') return;
    // Run resume directly inside a trusted pointer gesture, not after a network or
    // Unity callback. Safari can otherwise leave the musical/gameplay clock paused.
    if (!window.leftRightResumeAudio) {
      window.leftRightResumeAudio = function () {
        var context = WEBAudio.audioContext;
        if (context && context.state !== 'running') context.resume().catch(function () {});
      };
      document.addEventListener('pointerdown', window.leftRightResumeAudio, true);
      document.addEventListener('touchend', window.leftRightResumeAudio, true);
    }
    window.dispatchEvent(new CustomEvent('leftright-lobby', { detail: {
      code: UTF8ToString(code), status: UTF8ToString(status),
      humans: humans, host: !!host, playing: !!playing, x: x, jumping: !!jumping,
      menu: !!menu, menuPage: menu - 1, over: !!over, beat: beat, travel: travel, receiver: UTF8ToString(receiver)
    }}));
  }
});
