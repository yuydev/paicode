window.vibe = {
  scrollToBottom: function (id) {
    const el = document.getElementById(id);
    if (el) {
      el.scrollTop = el.scrollHeight;
    }
  }
};
