(() => {
    const SELECT_TYPES = new Set(["SingleSelect", "MultiSelect"]);
    const existingCriteria = Array.isArray(window.adminCriteriaExisting)
        ? window.adminCriteriaExisting
        : [];

    let pendingSimilarForm = null;
    let similarModal = null;

    function parseSeed(seedText) {
        if (!seedText || !seedText.trim()) {
            return [];
        }

        return seedText
            .split(/\r?\n/)
            .map(line => line.trim())
            .filter(line => line.length > 0)
            .map((line, index) => {
                const parts = line.split("|");
                const value = (parts[0] ?? "").trim();
                const label = (parts[1] ?? "").trim();
                const sortOrderText = (parts[2] ?? "").trim();
                const parsedSortOrder = Number.parseInt(sortOrderText, 10);

                return {
                    value,
                    label,
                    sortOrder: Number.isFinite(parsedSortOrder) && parsedSortOrder >= 0
                        ? parsedSortOrder
                        : index
                };
            })
            .sort((left, right) => left.sortOrder - right.sortOrder || left.value.localeCompare(right.value));
    }

    function createRow(value = "", label = "", sortOrder = 0) {
        const row = document.createElement("tr");
        row.innerHTML = `
            <td>
                <input type="text" class="form-control form-control-sm js-option-value" value="${escapeHtml(value)}" placeholder="Значение" />
            </td>
            <td>
                <input type="text" class="form-control form-control-sm js-option-label" value="${escapeHtml(label)}" placeholder="Подпись" />
            </td>
            <td>
                <input type="hidden" class="js-option-order" value="${Number.isFinite(sortOrder) && sortOrder >= 0 ? sortOrder : 0}" />
                <div class="d-flex align-items-center gap-1">
                    <span class="badge text-bg-light border js-option-order-label" style="min-width: 2.5rem;">${sortOrder + 1}</span>
                    <button type="button" class="btn btn-sm btn-outline-secondary js-move-option-up" title="Выше">↑</button>
                    <button type="button" class="btn btn-sm btn-outline-secondary js-move-option-down" title="Ниже">↓</button>
                </div>
            </td>
            <td class="text-end">
                <button type="button" class="btn btn-sm btn-outline-danger js-remove-option" title="Удалить">Удалить</button>
            </td>
        `;

        return row;
    }

    function escapeHtml(text) {
        return String(text ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;");
    }

    function ensureAtLeastOneRow(rowsContainer) {
        if (rowsContainer.children.length === 0) {
            rowsContainer.appendChild(createRow("", "", 0));
        }

        renumberRows(rowsContainer);
    }

    function renumberRows(rowsContainer) {
        const rows = Array.from(rowsContainer.querySelectorAll("tr"));
        rows.forEach((row, index) => {
            const orderInput = row.querySelector(".js-option-order");
            const orderLabel = row.querySelector(".js-option-order-label");
            const upButton = row.querySelector(".js-move-option-up");
            const downButton = row.querySelector(".js-move-option-down");

            if (orderInput) {
                orderInput.value = index.toString();
            }

            if (orderLabel) {
                orderLabel.textContent = (index + 1).toString();
            }

            if (upButton) {
                upButton.disabled = index === 0;
            }

            if (downButton) {
                downButton.disabled = index === rows.length - 1;
            }
        });
    }

    function toggleEditorState(form) {
        const valueTypeInput = form.querySelector(".js-criterion-value-type");
        const optionsEditor = form.querySelector(".js-options-editor");
        const optionsTextInput = form.querySelector(".js-options-text");
        const optionsRows = form.querySelector(".js-options-rows");
        const addOptionButton = form.querySelector(".js-add-option");

        if (!valueTypeInput || !optionsEditor || !optionsTextInput || !optionsRows || !addOptionButton) {
            return;
        }

        const isSelectType = SELECT_TYPES.has(valueTypeInput.value);
        optionsEditor.classList.toggle("d-none", !isSelectType);
        addOptionButton.disabled = !isSelectType;

        if (isSelectType) {
            ensureAtLeastOneRow(optionsRows);
            return;
        }

        optionsTextInput.value = "";
    }

    function collectOptions(form) {
        const optionsRows = form.querySelector(".js-options-rows");
        if (!optionsRows) {
            return { isValid: true, options: [] };
        }

        renumberRows(optionsRows);
        const rows = Array.from(optionsRows.querySelectorAll("tr"));
        const options = [];

        for (let index = 0; index < rows.length; index += 1) {
            const row = rows[index];
            const valueInput = row.querySelector(".js-option-value");
            const labelInput = row.querySelector(".js-option-label");
            const orderInput = row.querySelector(".js-option-order");
            if (!valueInput || !labelInput || !orderInput) {
                continue;
            }

            const value = valueInput.value.trim();
            const label = labelInput.value.trim();
            const orderValue = Number.parseInt(orderInput.value.trim(), 10);

            if (!value && !label) {
                continue;
            }

            if (!value || !label) {
                return {
                    isValid: false,
                    message: "Для каждой опции заполните и значение, и подпись."
                };
            }

            options.push({
                value,
                label,
                sortOrder: Number.isFinite(orderValue) && orderValue >= 0 ? orderValue : index
            });
        }

        return { isValid: true, options };
    }

    function collectLooseOptions(form) {
        const rows = Array.from(form.querySelectorAll(".js-options-rows tr"));
        return rows
            .map(row => {
                const value = row.querySelector(".js-option-value")?.value?.trim() ?? "";
                const label = row.querySelector(".js-option-label")?.value?.trim() ?? "";
                return { value, label };
            })
            .filter(option => option.value || option.label);
    }

    function hydrateForm(form) {
        const seedInput = form.querySelector(".js-options-seed");
        const rowsContainer = form.querySelector(".js-options-rows");
        const addOptionButton = form.querySelector(".js-add-option");
        const valueTypeInput = form.querySelector(".js-criterion-value-type");
        const optionsTextInput = form.querySelector(".js-options-text");
        const generateCodeButton = form.querySelector(".js-generate-code");
        const codeInput = form.querySelector(".js-criterion-code");
        const nameInput = form.querySelector(".js-criterion-name");

        if (!seedInput || !rowsContainer || !addOptionButton || !valueTypeInput || !optionsTextInput) {
            return;
        }

        const seedOptions = parseSeed(seedInput.value);
        rowsContainer.innerHTML = "";
        for (const option of seedOptions) {
            rowsContainer.appendChild(createRow(option.value, option.label, option.sortOrder));
        }

        if (seedOptions.length === 0) {
            rowsContainer.appendChild(createRow("", "", 0));
        }

        renumberRows(rowsContainer);

        addOptionButton.addEventListener("click", () => {
            rowsContainer.appendChild(createRow("", "", rowsContainer.children.length));
            renumberRows(rowsContainer);
        });

        if (generateCodeButton && codeInput && nameInput) {
            generateCodeButton.addEventListener("click", () => {
                codeInput.value = buildCode(nameInput.value);
                codeInput.focus();
                codeInput.dispatchEvent(new Event("input", { bubbles: true }));
            });
        }

        rowsContainer.addEventListener("click", event => {
            const target = event.target;
            if (!(target instanceof HTMLElement)) {
                return;
            }

            const row = target.closest("tr");
            if (!row) {
                return;
            }

            if (target.classList.contains("js-remove-option")) {
                row.remove();
                if (SELECT_TYPES.has(valueTypeInput.value)) {
                    ensureAtLeastOneRow(rowsContainer);
                }

                return;
            }

            if (target.classList.contains("js-move-option-up") && row.previousElementSibling) {
                rowsContainer.insertBefore(row, row.previousElementSibling);
                renumberRows(rowsContainer);
                return;
            }

            if (target.classList.contains("js-move-option-down") && row.nextElementSibling) {
                rowsContainer.insertBefore(row.nextElementSibling, row);
                renumberRows(rowsContainer);
            }
        });

        valueTypeInput.addEventListener("change", () => toggleEditorState(form));

        form.addEventListener("submit", event => {
            if (!validateCode(form)) {
                delete form.dataset.skipSimilar;
                event.preventDefault();
                return;
            }

            if (form.dataset.checkSimilar === "true" && form.dataset.skipSimilar !== "true") {
                const similar = findSimilarCriterion(form);
                if (similar) {
                    event.preventDefault();
                    showSimilarModal(form, similar);
                    return;
                }
            }

            delete form.dataset.skipSimilar;

            const isSelectType = SELECT_TYPES.has(valueTypeInput.value);
            if (!isSelectType) {
                optionsTextInput.value = "";
                return;
            }

            const collected = collectOptions(form);
            if (!collected.isValid) {
                event.preventDefault();
                alert(collected.message);
                return;
            }

            if (collected.options.length === 0) {
                event.preventDefault();
                alert("Добавьте хотя бы одну опцию для типа выбора.");
                return;
            }

            optionsTextInput.value = collected.options
                .map(option => `${option.value}|${option.label}|${option.sortOrder}`)
                .join("\n");
        });

        toggleEditorState(form);
    }

    function validateCode(form) {
        const codeInput = form.querySelector(".js-criterion-code");
        if (!codeInput) {
            return true;
        }

        const currentId = (form.dataset.criterionId ?? "").toLowerCase();
        const code = codeInput.value.trim().toLowerCase();
        const duplicate = existingCriteria.find(item =>
            String(item.id ?? "").toLowerCase() !== currentId
            && String(item.code ?? "").trim().toLowerCase() === code);

        codeInput.setCustomValidity("");
        if (!duplicate) {
            return true;
        }

        codeInput.setCustomValidity("Критерий с таким кодом уже существует.");
        codeInput.reportValidity();
        return false;
    }

    function buildCode(value) {
        const transliterated = transliterate(value)
            .toLowerCase()
            .replace(/[^a-z0-9_-]+/g, "_")
            .replace(/_+/g, "_")
            .replace(/^_+|_+$/g, "");

        return (transliterated || "criterion").slice(0, 100);
    }

    function transliterate(value) {
        const map = {
            а: "a", б: "b", в: "v", г: "g", д: "d", е: "e", ё: "e", ж: "zh", з: "z",
            и: "i", й: "y", к: "k", л: "l", м: "m", н: "n", о: "o", п: "p", р: "r",
            с: "s", т: "t", у: "u", ф: "f", х: "h", ц: "c", ч: "ch", ш: "sh", щ: "sch",
            ъ: "", ы: "y", ь: "", э: "e", ю: "yu", я: "ya"
        };

        return String(value ?? "")
            .split("")
            .map(char => map[char.toLowerCase()] ?? char)
            .join("");
    }

    function getCriterionFromForm(form) {
        return {
            code: form.querySelector(".js-criterion-code")?.value?.trim() ?? "",
            displayName: form.querySelector(".js-criterion-name")?.value?.trim() ?? "",
            description: form.querySelector(".js-criterion-description")?.value?.trim() ?? "",
            valueType: form.querySelector(".js-criterion-value-type")?.value ?? "",
            options: collectLooseOptions(form)
        };
    }

    function findSimilarCriterion(form) {
        const current = getCriterionFromForm(form);
        const currentWords = getWords([
            current.displayName,
            current.description,
            ...current.options.flatMap(option => [option.value, option.label])
        ].join(" "));

        if (currentWords.size === 0) {
            return null;
        }

        let best = null;
        let bestScore = 0;

        for (const item of existingCriteria) {
            const existingWords = getWords([
                item.displayName,
                item.description,
                ...(item.options ?? []).flatMap(option => [option.value, option.label])
            ].join(" "));

            const score = similarityScore(current, item, currentWords, existingWords);
            if (score > bestScore) {
                bestScore = score;
                best = item;
            }
        }

        return bestScore >= 0.58 ? best : null;
    }

    function similarityScore(current, existing, currentWords, existingWords) {
        const nameA = normalizeText(current.displayName);
        const nameB = normalizeText(existing.displayName);
        const descriptionA = normalizeText(current.description);
        const descriptionB = normalizeText(existing.description);
        const optionScore = optionOverlap(current.options, existing.options ?? []);
        const wordScore = wordOverlap(currentWords, existingWords);

        if (nameA && nameA === nameB) {
            return 1;
        }

        if (nameA.length >= 4 && nameB.length >= 4 && (nameA.includes(nameB) || nameB.includes(nameA))) {
            return 0.82;
        }

        if (descriptionA && descriptionA === descriptionB) {
            return 0.75;
        }

        return Math.max(wordScore, optionScore);
    }

    function normalizeText(value) {
        return String(value ?? "")
            .toLowerCase()
            .replaceAll("ё", "е")
            .replace(/[^a-zа-я0-9]+/gi, " ")
            .trim()
            .replace(/\s+/g, " ");
    }

    function getWords(value) {
        const stopWords = new Set(["для", "или", "что", "это", "как", "при", "the", "and", "with"]);
        return new Set(normalizeText(value)
            .split(" ")
            .filter(word => word.length >= 3 && !stopWords.has(word)));
    }

    function wordOverlap(left, right) {
        if (left.size === 0 || right.size === 0) {
            return 0;
        }

        const intersection = [...left].filter(word => right.has(word)).length;
        return intersection / Math.min(left.size, right.size);
    }

    function optionOverlap(left, right) {
        const leftWords = getWords(left.flatMap(option => [option.value, option.label]).join(" "));
        const rightWords = getWords(right.flatMap(option => [option.value, option.label]).join(" "));

        if (leftWords.size < 2 || rightWords.size < 2) {
            return 0;
        }

        return wordOverlap(leftWords, rightWords);
    }

    function showSimilarModal(form, existing) {
        pendingSimilarForm = form;
        const current = getCriterionFromForm(form);
        const newContainer = document.getElementById("similarCriterionNew");
        const existingContainer = document.getElementById("similarCriterionExisting");
        const modalElement = document.getElementById("similarCriterionModal");

        if (!newContainer || !existingContainer || !modalElement) {
            if (confirm("Существует похожий критерий. Все равно создать?")) {
                submitIgnoringSimilar(form);
            }

            return;
        }

        newContainer.innerHTML = renderCriterionPreview(current);
        existingContainer.innerHTML = renderCriterionPreview(existing);

        if (window.bootstrap?.Modal) {
            similarModal ??= new window.bootstrap.Modal(modalElement);
            similarModal.show();
            return;
        }

        modalElement.classList.add("show");
        modalElement.style.display = "block";
    }

    function renderCriterionPreview(item) {
        const options = item.options ?? [];
        const optionText = options.length > 0
            ? options.map(option => escapeHtml(option.label || option.value)).join(", ")
            : "нет";

        return `
            <div class="mb-1"><span class="text-muted">Код:</span> ${escapeHtml(item.code)}</div>
            <div class="mb-1"><span class="text-muted">Название:</span> ${escapeHtml(item.displayName)}</div>
            <div class="mb-1"><span class="text-muted">Тип:</span> ${escapeHtml(item.valueType)}</div>
            <div class="mb-1"><span class="text-muted">Описание:</span> ${escapeHtml(item.description || "нет")}</div>
            <div><span class="text-muted">Опции:</span> ${optionText}</div>
        `;
    }

    function submitIgnoringSimilar(form) {
        form.dataset.skipSimilar = "true";
        form.requestSubmit();
    }

    document.addEventListener("DOMContentLoaded", () => {
        const forms = document.querySelectorAll(".js-criterion-form");
        for (const form of forms) {
            hydrateForm(form);
        }

        document.getElementById("createSimilarCriterionAnyway")?.addEventListener("click", () => {
            if (!pendingSimilarForm) {
                return;
            }

            if (similarModal) {
                similarModal.hide();
            }

            submitIgnoringSimilar(pendingSimilarForm);
            pendingSimilarForm = null;
        });
    });
})();
