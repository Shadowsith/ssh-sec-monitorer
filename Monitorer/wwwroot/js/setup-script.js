// Card "Nutzer auf VM anlegen": fills the placeholders of setup-sftp-user.sh in the browser.
// Values are validated strictly, because they end up inside double-quoted bash strings on the VM.
class SetupScriptCard {
    static PLACEHOLDERS = {
        dir: '<PFAD_ZUM_AGENT_DATENORDNER>',
        key: '<OEFFENTLICHER_SCHLUESSEL_DES_MONITORERS>',
        ip: '<IP_DES_MONITORERS>'
    };

    static init() {
        document.querySelectorAll('[data-setup-script]').forEach(box => new SetupScriptCard(box));
    }

    constructor(box) {
        this.box = box;
        this.form = document.querySelector(box.dataset.form);
        this.pre = box.querySelector('#setup-script');
        this.variant = box.querySelector('[data-script-variant]');
        this.fileLabel = box.querySelector('[data-script-filename]');
        this.variant.addEventListener('change', () => this.switchVariant(true));
        try {
            const stored = localStorage.getItem('ssm-setup-variant');
            if (stored && [...this.variant.options].some(o => o.value === stored)) this.variant.value = stored;
        } catch (e) { /* ignore */ }
        this.switchVariant(false);
        this.status = box.querySelector('[data-script-status]');
        box.querySelector('[data-script-apply]').addEventListener('click', () => this.apply());
        box.querySelector('[data-script-reset]').addEventListener('click', () => this.reset());
        box.querySelector('[data-script-derive]').addEventListener('click', () => this.derive(true));
        box.querySelector('[data-script-download]').addEventListener('click', () => this.download());
        // saved credential with key: fill the public key right away
        if (box.dataset.hasKey === 'true') this.derive(false);
    }

    /** Shows the script of the selected system; already filled values are applied to it again. */
    switchVariant(userChange) {
        const source = this.box.querySelector(`[data-setup-template="${this.variant.value}"]`);
        if (!source) return;
        this.template = source.textContent;
        this.fileName = this.variant.selectedOptions[0].dataset.file;
        this.fileLabel.textContent = this.fileName;
        try { localStorage.setItem('ssm-setup-variant', this.variant.value); } catch (e) { /* ignore */ }
        if (this.applied) this.apply();
        else this.pre.textContent = this.template;
        if (userChange && !this.applied) this.say(`Skript für ${this.variant.selectedOptions[0].textContent}.`, true);
    }

    field(name) {
        return this.box.querySelector(`[data-script-field="${name}"]`);
    }

    /** Returns an error text or null. */
    static validate(dir, key, ip) {
        if (!/^\/[A-Za-z0-9._\/-]+$/.test(dir) || dir.includes('..') || dir.replace(/\/+$/, '').split('/').length < 3)
            return 'Agent-Datenordner: absoluter Pfad mit mindestens zwei Ebenen, nur Buchstaben, Ziffern, . _ - / (z. B. /opt/sshsecmonitorer/agent).';
        if (!/^(ssh-ed25519|ssh-rsa|ecdsa-sha2-nistp(256|384|521)) [A-Za-z0-9+\/=]+( [A-Za-z0-9._@+-]*)?$/.test(key))
            return 'Öffentlicher Schlüssel: eine Zeile wie „ssh-ed25519 AAAA… kommentar“.';
        if (ip && !/^[0-9A-Fa-f.:\/,*]+$/.test(ip))
            return 'IP des Monitorers: nur IP-Adressen / Netze, mehrere mit Komma.';
        return null;
    }

    apply() {
        const dir = this.field('dir').value.trim().replace(/\/+$/, '');
        const key = this.field('key').value.trim();
        const ip = this.field('ip').value.trim().replace(/\s+/g, '');
        const error = SetupScriptCard.validate(dir, key, ip);
        if (error) return this.say(error, false);

        const P = SetupScriptCard.PLACEHOLDERS;
        this.pre.textContent = this.template
            .replace(`MONITORING_DIR="${P.dir}"`, `MONITORING_DIR="${dir}"`)
            .replace(`PUBLIC_KEY="${P.key}"`, `PUBLIC_KEY="${key}"`)
            .replace(`MONITORER_IP="${P.ip}"`, `MONITORER_IP="${ip}"`);
        this.applied = true;
        const path = '/' + dir.split('/').pop() + '/monitoring.json';
        this.say(`Übernommen. Pfad für den Agent im Monitorer: ${path} (relativ zum Chroot).`, true);
    }

    reset() {
        this.applied = false;
        this.pre.textContent = this.template;
        this.say('Platzhalter wiederhergestellt.', true);
    }

    async derive(manual) {
        const token = this.form.querySelector('input[name="__RequestVerificationToken"]');
        const keyField = this.form.querySelector('[name="Input.PrivateKey"]');
        const passField = this.form.querySelector('[name="Input.Passphrase"]');
        try {
            const response = await fetch('/api/ssh-credentials/public-key', {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token ? token.value : '' },
                body: JSON.stringify({
                    credentialId: this.box.dataset.credentialId ? Number(this.box.dataset.credentialId) : null,
                    privateKey: keyField && keyField.value.trim() ? keyField.value : null,
                    passphrase: passField && passField.value ? passField.value : null
                })
            });
            const text = await response.text();
            if (!response.ok) return manual && this.say(text || `Fehler ${response.status}`, false);
            // plain text from the API (string result); tolerate a JSON string as well
            this.field('key').value = text.startsWith('"') ? JSON.parse(text) : text.trim();
            if (manual) this.say('Öffentlicher Schlüssel abgeleitet – jetzt „Übernehmen“.', true);
        } catch (e) {
            if (manual) this.say('Der Monitorer ist nicht erreichbar.', false);
        }
    }

    download() {
        const blob = new Blob([this.pre.textContent], { type: 'text/x-shellscript' });
        const link = document.createElement('a');
        link.href = URL.createObjectURL(blob);
        link.download = this.fileName || 'setup-sftp-user.sh';
        document.body.append(link);
        link.click();
        link.remove();
        setTimeout(() => URL.revokeObjectURL(link.href), 1000);
    }

    say(text, ok) {
        this.status.textContent = text;
        this.status.className = `small ${ok ? 'text-success' : 'text-danger'}`;
    }
}

document.addEventListener('DOMContentLoaded', SetupScriptCard.init);
