// "Test connection" on the SSH credential page: sends the current (also unsaved) form values to
// POST api/ssh-credentials/test. The session cookie authenticates, the antiforgery token of the form goes
// into the X-CSRF-TOKEN header (checked by ApiAntiforgeryFilter). Secrets are only sent, never displayed.
class SshConnectionTest {
    static init() {
        document.querySelectorAll('[data-ssh-test]').forEach(box => new SshConnectionTest(box));
    }

    constructor(box) {
        this.box = box;
        this.form = document.querySelector(box.dataset.form);
        this.result = box.querySelector('[data-test-result]');
        this.button = box.querySelector('[data-test-run]');
        this.button.addEventListener('click', () => this.run());
        // picking a known agent host also fills port and path
        const host = this.field('host');
        host.addEventListener('change', () => {
            const option = [...document.querySelectorAll('#test-targets option')].find(o => o.value === host.value);
            if (option) {
                this.field('port').value = option.dataset.port;
                this.field('path').value = option.dataset.path;
            }
        });
    }

    field(name) {
        return this.box.querySelector(`[data-test-field="${name}"]`);
    }

    formValue(name) {
        const el = this.form.querySelector(`[name="Input.${name}"]:not([type=radio]), [name="Input.${name}"][type=radio]:checked`);
        return el ? el.value : '';
    }

    async run() {
        const token = this.form.querySelector('input[name="__RequestVerificationToken"]');
        const body = {
            credentialId: this.box.dataset.credentialId ? Number(this.box.dataset.credentialId) : null,
            userName: this.formValue('UserName'),
            authType: this.formValue('AuthType'),
            privateKey: this.formValue('PrivateKey') || null,
            passphrase: this.formValue('Passphrase') || null,
            password: this.formValue('Password') || null,
            host: this.field('host').value.trim(),
            port: Number(this.field('port').value) || 22,
            remotePath: this.field('path').value.trim() || null
        };
        this.busy(true);
        try {
            const response = await fetch('/api/ssh-credentials/test', {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token ? token.value : '' },
                body: JSON.stringify(body)
            });
            if (response.ok) this.show(await response.json());
            else this.error(await response.text() || I18n.t('js.error.status', response.status));
        } catch (e) {
            this.error(I18n.t('js.error.unreachable'));
        } finally {
            this.busy(false);
        }
    }

    busy(on) {
        this.button.disabled = on;
        this.box.querySelector('[data-test-spinner]').classList.toggle('d-none', !on);
        this.box.querySelector('[data-test-icon]').classList.toggle('d-none', on);
        if (on) this.result.replaceChildren(SshConnectionTest.el('div', 'small text-body-secondary', I18n.t('js.sshTest.running')));
    }

    show(result) {
        const list = SshConnectionTest.el('ul', 'list-group mb-2');
        result.steps.forEach(step => {
            const item = SshConnectionTest.el('li', 'list-group-item d-flex gap-2 align-items-start');
            item.append(
                SshConnectionTest.el('i', `bi ${step.ok ? 'bi-check-circle-fill text-success' : 'bi-x-circle-fill text-danger'} mt-1`),
                SshConnectionTest.el('div', '', '', [
                    SshConnectionTest.el('div', 'fw-semibold', step.step),
                    SshConnectionTest.el('div', 'small text-body-secondary text-break', step.message)
                ]));
            list.append(item);
        });
        const summary = SshConnectionTest.el('div', `alert ${result.success ? 'alert-success' : 'alert-danger'} py-2 mb-2`,
            I18n.t(result.success ? 'js.sshTest.success' : 'js.sshTest.failed'));
        this.result.replaceChildren(summary, list);
    }

    error(text) {
        this.result.replaceChildren(SshConnectionTest.el('div', 'alert alert-danger py-2 mb-0', text));
    }

    /** Builds an element with textContent only - server texts are never inserted as HTML. */
    static el(tag, className, text = '', children = []) {
        const e = document.createElement(tag);
        if (className) e.className = className;
        if (text) e.textContent = text;
        children.forEach(c => e.append(c));
        return e;
    }
}

document.addEventListener('DOMContentLoaded', SshConnectionTest.init);
