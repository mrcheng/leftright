mergeInto(LibraryManager.library, {
  LeftRight_UpdateLobby: function (code, status, humans, host, playing, x, jumping) {
    if (typeof window === 'undefined') return;
    window.dispatchEvent(new CustomEvent('leftright-lobby', { detail: {
      code: UTF8ToString(code), status: UTF8ToString(status),
      humans: humans, host: !!host, playing: !!playing, x: x, jumping: !!jumping
    }}));
  }
});
