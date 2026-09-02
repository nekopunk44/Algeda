(function (global) {
    function initExplicitSubmitForms() {
        const forms = document.querySelectorAll("form[data-explicit-submit='true']");
        if (forms.length === 0) {
            return;
        }

        forms.forEach((form) => {
            form.addEventListener("keydown", (event) => {
                if (event.key !== "Enter") {
                    return;
                }

                const target = event.target;
                if (!(target instanceof HTMLElement)) {
                    return;
                }

                if (target instanceof HTMLTextAreaElement || target.isContentEditable) {
                    return;
                }

                if (target.dataset.allowEnter === "true") {
                    return;
                }

                event.preventDefault();
            });
        });
    }

    global.initExplicitSubmitForms = initExplicitSubmitForms;

    document.addEventListener("DOMContentLoaded", () => {
        initExplicitSubmitForms();
    });
})(window);
