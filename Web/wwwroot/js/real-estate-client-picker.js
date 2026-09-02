document.addEventListener("DOMContentLoaded", function () {
    const toggle = document.getElementById("unregisteredOwnerToggle");
    const registeredContainer = document.getElementById("registeredOwnerContainer");
    const unregisteredContainer = document.getElementById("unregisteredOwnerContainer");

    const ownerClientIdInput = document.getElementById("ownerClientIdInput");
    const ownerClientNameDisplay = document.getElementById("ownerClientNameDisplay");

    const manualOwnerFullName = document.getElementById("manualOwnerFullName");
    const manualOwnerEmail = document.getElementById("manualOwnerEmail");
    const manualOwnerPhoneNumber = document.getElementById("manualOwnerPhoneNumber");

    // Called ONLY when user actively changes the toggle
    function updateVisibilityOnChange() {
        if (toggle.checked) {
            // switching TO manual mode — clear client selection, show manual fields
            registeredContainer.classList.add("d-none");
            unregisteredContainer.classList.remove("d-none");
            ownerClientIdInput.value = "";
            ownerClientNameDisplay.value = "Не выбран";
            manualOwnerFullName.setAttribute("required", "required");
            manualOwnerEmail.setAttribute("required", "required");
            manualOwnerPhoneNumber.setAttribute("required", "required");
        } else {
            // switching TO client mode — hide manual fields, do NOT clear text so user can undo
            registeredContainer.classList.remove("d-none");
            unregisteredContainer.classList.add("d-none");
            manualOwnerFullName.removeAttribute("required");
            manualOwnerEmail.removeAttribute("required");
            manualOwnerPhoneNumber.removeAttribute("required");
        }
    }

    // Called on page init — applies visibility without clearing data
    function initVisibility() {
        if (toggle.checked) {
            registeredContainer.classList.add("d-none");
            unregisteredContainer.classList.remove("d-none");
            manualOwnerFullName.setAttribute("required", "required");
            manualOwnerEmail.setAttribute("required", "required");
            manualOwnerPhoneNumber.setAttribute("required", "required");
        } else {
            registeredContainer.classList.remove("d-none");
            unregisteredContainer.classList.add("d-none");
            manualOwnerFullName.removeAttribute("required");
            manualOwnerEmail.removeAttribute("required");
            manualOwnerPhoneNumber.removeAttribute("required");
        }
    }

    if (toggle) {
        toggle.addEventListener("change", updateVisibilityOnChange);
        initVisibility();
    }

    // Modal Logic
    const searchInput = document.getElementById("clientSearchInput");
    const listContainer = document.getElementById("clientPickerList");
    const paginationContainer = document.getElementById("clientPickerPagination");
    const loadingSpinner = document.getElementById("clientSearchLoading");

    let allClients = [];
    let filteredClients = [];
    let currentPage = 1;
    const itemsPerPage = 10;
    let dataLoaded = false;

    const modalElement = document.getElementById('clientSearchModal');
    if (modalElement) {
        modalElement.addEventListener('show.bs.modal', async function () {
            if (!dataLoaded) {
                loadingSpinner.classList.remove("d-none");
                listContainer.innerHTML = "";
                try {
                    const response = await fetch('/api/clients?limit=500');
                    if (response.ok) {
                        allClients = await response.json();
                        filteredClients = [...allClients];
                        dataLoaded = true;
                        renderList();
                    } else {
                        listContainer.innerHTML = '<div class="alert alert-danger">Ошибка загрузки клиентов (' + response.status + ').</div>';
                    }
                } catch (e) {
                    listContainer.innerHTML = '<div class="alert alert-danger">Ошибка сети. Проверьте соединение.</div>';
                } finally {
                    loadingSpinner.classList.add("d-none");
                }
            }
        });
    }

    if (searchInput) {
        searchInput.addEventListener("input", function (e) {
            const query = e.target.value.toLowerCase();
            filteredClients = allClients.filter(c => {
                const name = `${c.firstName ?? ''} ${c.lastName ?? ''}`.toLowerCase();
                const email = (c.email ?? '').toLowerCase();
                const phone = (c.phoneNumber ?? '').toLowerCase();
                return name.includes(query) || email.includes(query) || phone.includes(query);
            });
            currentPage = 1;
            renderList();
        });
    }

    function renderList() {
        listContainer.innerHTML = "";

        if (filteredClients.length === 0) {
            listContainer.innerHTML = '<div class="text-muted p-3 text-center">Ничего не найдено</div>';
            if (paginationContainer) paginationContainer.innerHTML = "";
            return;
        }

        const startIndex = (currentPage - 1) * itemsPerPage;
        const endIndex = startIndex + itemsPerPage;
        const pageItems = filteredClients.slice(startIndex, endIndex);

        pageItems.forEach(client => {
            const name = `${client.firstName ?? ''} ${client.lastName ?? ''}`.trim();
            const displayName = name || 'Без имени';
            const btn = document.createElement("button");
            btn.type = "button";
            btn.className = "list-group-item list-group-item-action d-flex justify-content-between align-items-center";
            btn.innerHTML = `
                <div>
                    <strong>${displayName}</strong><br/>
                    <small class="text-muted">${client.email || 'Нет email'} | ${client.phoneNumber || ''}</small>
                </div>
                <span class="btn btn-sm btn-primary">Выбрать</span>
            `;

            btn.addEventListener("click", () => {
                ownerClientIdInput.value = client.id;
                const displayParts = [];
                if (displayName) displayParts.push(displayName);
                if (client.email) displayParts.push(client.email);
                if (client.phoneNumber) displayParts.push(client.phoneNumber);
                ownerClientNameDisplay.value = displayParts.length > 0 ? displayParts.join(' | ') : 'Не выбран';

                // Also populate manual fields for display purposes
                if (manualOwnerFullName) manualOwnerFullName.value = displayName;
                if (manualOwnerEmail) manualOwnerEmail.value = client.email || '';
                if (manualOwnerPhoneNumber) manualOwnerPhoneNumber.value = client.phoneNumber || '';

                const bsModal = bootstrap.Modal.getInstance(modalElement);
                if (bsModal) bsModal.hide();
            });

            listContainer.appendChild(btn);
        });

        renderPagination();
    }

    function renderPagination() {
        if (!paginationContainer) return;
        const totalPages = Math.ceil(filteredClients.length / itemsPerPage);
        paginationContainer.innerHTML = "";

        if (totalPages <= 1) return;

        for (let i = 1; i <= totalPages; i++) {
            const li = document.createElement("li");
            li.className = `page-item ${i === currentPage ? 'active' : ''}`;
            const a = document.createElement("a");
            a.className = "page-link";
            a.href = "#";
            a.innerText = i;
            a.addEventListener("click", (e) => {
                e.preventDefault();
                currentPage = i;
                renderList();
            });
            li.appendChild(a);
            paginationContainer.appendChild(li);
        }
    }
});
