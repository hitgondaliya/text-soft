// Marketing site interactions (FR-7): mobile nav + footer year.
(function () {
    var toggle = document.querySelector('.nav__toggle');
    var links = document.querySelector('.nav__links');

    if (toggle && links) {
        toggle.addEventListener('click', function () {
            var open = links.classList.toggle('open');
            toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
        });
        // Close the menu after tapping a link on mobile.
        links.addEventListener('click', function (e) {
            if (e.target.tagName === 'A') {
                links.classList.remove('open');
                toggle.setAttribute('aria-expanded', 'false');
            }
        });
    }

    var year = document.getElementById('year');
    if (year) year.textContent = new Date().getFullYear();
})();
