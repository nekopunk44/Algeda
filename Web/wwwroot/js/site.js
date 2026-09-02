// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

document.addEventListener("DOMContentLoaded", function () {
    const currentUrl = location.pathname;
    const navLinks = document.querySelectorAll('.navbar-nav .nav-link');

    navLinks.forEach(link => {
        const href = link.getAttribute('href');
        if (!href) return;

        // Exact match for home, or starts-with for other sections
        if (href === currentUrl) {
            link.classList.add('active');
        } else if (href !== '/' && currentUrl.startsWith(href)) {
            link.classList.add('active');
        }
    });

    document.querySelectorAll('.password-toggle').forEach(button => {
        button.addEventListener('click', () => {
            const group = button.closest('.password-input-group');
            const input = group?.querySelector('input');
            const icon = button.querySelector('i');

            if (!input) return;

            const shouldShow = input.type === 'password';
            input.type = shouldShow ? 'text' : 'password';
            button.setAttribute('aria-label', shouldShow ? 'Скрыть пароль' : 'Показать пароль');
            button.setAttribute('title', shouldShow ? 'Скрыть пароль' : 'Показать пароль');

            if (icon) {
                icon.classList.toggle('bi-eye', !shouldShow);
                icon.classList.toggle('bi-eye-slash', shouldShow);
            }
        });
    });
});
