// Agent edit page: shows the fields of the selected connection type (SFTP or local).
// Hidden fields are disabled so the browser neither validates nor posts them.
(() => {
    "use strict";

    class ConnectionTypeSwitch {
        constructor(form) {
            this.form = form;
            this.radios = [...form.querySelectorAll("[data-connection-type]")];
            this.radios.forEach((r) => r.addEventListener("change", () => this.apply()));
            this.apply();
        }

        get selected() {
            return this.radios.find((r) => r.checked)?.value ?? "Ssh";
        }

        apply() {
            const type = this.selected;
            document.querySelectorAll("[data-show-for]").forEach((el) => {
                const visible = el.dataset.showFor === type;
                el.classList.toggle("d-none", !visible);
                el.querySelectorAll("input, select, textarea").forEach((field) => {
                    field.disabled = !visible;
                });
                if (el.matches("button")) {
                    el.disabled = !visible;
                }
            });
        }
    }

    document.querySelectorAll("[data-connection-form]").forEach((form) => new ConnectionTypeSwitch(form));
})();
