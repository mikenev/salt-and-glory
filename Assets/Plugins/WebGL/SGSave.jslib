// Salt and Glory save slots: browser localStorage (synchronous), keys "saltglory.slot1".."saltglory.slot3".
mergeInto(LibraryManager.library, {
  SG_SaveSet: function (k, v) { try { localStorage.setItem(UTF8ToString(k), UTF8ToString(v)); return 1; } catch (e) { return 0; } },
  SG_SaveGet: function (k) {
    var s = null; try { s = localStorage.getItem(UTF8ToString(k)); } catch (e) {}
    if (s === null) s = "";
    var n = lengthBytesUTF8(s) + 1, b = _malloc(n); stringToUTF8(s, b, n); return b;
  }
});
