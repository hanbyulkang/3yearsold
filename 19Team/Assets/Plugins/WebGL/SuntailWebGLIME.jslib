mergeInto(LibraryManager.library, {
  SuntailIMEBegin: function(valuePtr, multiline) {
    var old = document.getElementById('suntail-webgl-ime');
    if (old) old.remove();

    var input = document.createElement(multiline ? 'textarea' : 'input');
    input.id = 'suntail-webgl-ime';
    input.value = UTF8ToString(valuePtr);
    input.setAttribute('autocomplete', 'off');
    input.setAttribute('autocapitalize', 'off');
    input.setAttribute('spellcheck', 'false');
    input.style.position = 'fixed';
    input.style.left = '50%';
    input.style.top = '50%';
    input.style.width = '2px';
    input.style.height = '2px';
    input.style.opacity = '0.01';
    input.style.zIndex = '2147483647';
    input.style.border = '0';
    input.style.padding = '0';
    input.style.fontSize = '16px';
    input.style.background = 'transparent';

    var sendValue = function(method) {
      if (typeof unityInstance !== 'undefined' && unityInstance)
        unityInstance.SendMessage('WebGLKoreanInputBridge', method, input.value);
      else if (typeof SendMessage !== 'undefined')
        SendMessage('WebGLKoreanInputBridge', method, input.value);
    };
    input.addEventListener('input', function() { sendValue('OnIMEValue'); });
    input.addEventListener('compositionend', function() { sendValue('OnIMEValue'); });
    input.addEventListener('blur', function() { sendValue('OnIMEEnd'); });
    document.body.appendChild(input);
    input.focus({ preventScroll: true });
    input.setSelectionRange(input.value.length, input.value.length);
  },

  SuntailIMEEnd: function() {
    var input = document.getElementById('suntail-webgl-ime');
    if (!input) return;
    if (typeof unityInstance !== 'undefined' && unityInstance)
      unityInstance.SendMessage('WebGLKoreanInputBridge', 'OnIMEEnd', input.value);
    else if (typeof SendMessage !== 'undefined')
      SendMessage('WebGLKoreanInputBridge', 'OnIMEEnd', input.value);
    input.remove();
  }
});
