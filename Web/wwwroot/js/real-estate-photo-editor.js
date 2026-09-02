(function (global) {
    function realEstatePhotoEditorInit(config) {
        if (!config || typeof config !== "object") {
            return;
        }

        const list = document.getElementById(config.photosListId || "");
        const retainInputsContainer = document.getElementById(config.retainInputsContainerId || "");
        const retainedCount = document.getElementById(config.retainedCountId || "");

        if (!(list instanceof HTMLElement) || !(retainInputsContainer instanceof HTMLElement)) {
            return;
        }

        list.addEventListener("click", (event) => {
            const target = event.target;
            if (!(target instanceof HTMLElement)) {
                return;
            }

            const card = target.closest(".js-photo-card");
            if (!(card instanceof HTMLElement)) {
                return;
            }

            if (target.classList.contains("js-photo-delete")) {
                toggleDelete(card, target);
                syncRetainedInputs();
                return;
            }

            if (target.classList.contains("js-photo-move-up")) {
                moveCard(card, -1);
                syncRetainedInputs();
                return;
            }

            if (target.classList.contains("js-photo-move-down")) {
                moveCard(card, 1);
                syncRetainedInputs();
            }
        });

        syncRetainedInputs();

        function toggleDelete(card, button) {
            const isDeleted = card.dataset.deleted === "true";
            const nextDeleted = !isDeleted;
            card.dataset.deleted = nextDeleted ? "true" : "false";
            card.classList.toggle("opacity-50", nextDeleted);

            const note = card.querySelector(".js-photo-delete-note");
            if (note instanceof HTMLElement) {
                note.classList.toggle("d-none", !nextDeleted);
            }

            if (button instanceof HTMLElement) {
                button.textContent = nextDeleted ? "Вернуть" : "Удалить";
                button.classList.toggle("btn-outline-danger", !nextDeleted);
                button.classList.toggle("btn-warning", nextDeleted);
            }
        }

        function moveCard(card, direction) {
            const parent = card.parentElement;
            if (!parent) {
                return;
            }

            if (direction < 0) {
                const previous = card.previousElementSibling;
                if (previous) {
                    parent.insertBefore(card, previous);
                }
                return;
            }

            const next = card.nextElementSibling;
            if (next && next.nextSibling) {
                parent.insertBefore(next, card);
                return;
            }

            if (next) {
                parent.insertBefore(next, card);
                parent.appendChild(card);
            }
        }

        function syncRetainedInputs() {
            retainInputsContainer.innerHTML = "";

            const cards = Array.from(list.querySelectorAll(".js-photo-card"));
            const retained = cards
                .filter((cardElement) => cardElement instanceof HTMLElement && cardElement.dataset.deleted !== "true")
                .map((cardElement) => String(cardElement.getAttribute("data-photo-path") || "").trim())
                .filter(Boolean);

            retained.forEach((path) => {
                const input = document.createElement("input");
                input.type = "hidden";
                input.name = "retainPhotoPaths";
                input.value = path;
                retainInputsContainer.appendChild(input);
            });

            if (retainedCount instanceof HTMLElement) {
                retainedCount.textContent = String(retained.length);
            }
        }
    }

    global.realEstatePhotoEditorInit = realEstatePhotoEditorInit;
})(window);
