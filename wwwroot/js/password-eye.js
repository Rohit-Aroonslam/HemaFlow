// Puts the show/hide eye INSIDE every password box, on every page.
// - Existing Bootstrap input-groups (input + eye button) are restyled so the button sits inside the box.
// - Plain password inputs get an eye added automatically.
(function () {
    var css = [
        '.input-group.pw-inside{position:relative;flex-wrap:nowrap}',
        '.input-group.pw-inside>.form-control{width:100%;padding-right:2.75rem;border-radius:var(--bs-border-radius,.375rem)!important}',
        '.input-group.pw-inside>.btn{position:absolute;top:0;right:0;bottom:0;z-index:5;width:2.75rem;display:flex;align-items:center;justify-content:center;padding:0;border:0!important;background:transparent!important;box-shadow:none!important;color:#64748b;border-radius:0 var(--bs-border-radius,.375rem) var(--bs-border-radius,.375rem) 0!important}',
        '.input-group.pw-inside>.btn:hover{color:#0f172a}',
        '.pw-field{position:relative}',
        '.pw-field>.form-control{padding-right:2.75rem}',
        '.pw-field>.pw-eye{position:absolute;top:0;right:0;height:38px;width:2.75rem;display:flex;align-items:center;justify-content:center;padding:0;border:0;background:transparent;color:#64748b}',
        '.pw-field>.pw-eye:hover{color:#0f172a}',
        'input[type=password]::-ms-reveal,input[type=password]::-ms-clear{display:none}'
    ].join('\n');
    var st = document.createElement('style'); st.textContent = css; document.head.appendChild(st);

    function setup() {
        document.querySelectorAll('input[type=password]').forEach(function (input) {
            if (input.dataset.pwEye) return;
            input.dataset.pwEye = '1';
            var group = input.closest('.input-group');
            if (group && group.querySelector('.btn .bi-eye, .btn .bi-eye-slash')) {
                group.classList.add('pw-inside');
                return;
            }
            var wrap = document.createElement('div');
            wrap.className = 'pw-field';
            input.parentNode.insertBefore(wrap, input);
            wrap.appendChild(input);
            var b = document.createElement('button');
            b.type = 'button'; b.className = 'pw-eye'; b.tabIndex = -1;
            b.setAttribute('aria-label', 'Show or hide password');
            b.innerHTML = '<i class="bi bi-eye-slash"></i>';
            b.addEventListener('click', function () {
                var show = input.type === 'password';
                input.type = show ? 'text' : 'password';
                b.firstChild.className = 'bi ' + (show ? 'bi-eye' : 'bi-eye-slash');
            });
            wrap.appendChild(b);
        });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', setup); else setup();
})();
