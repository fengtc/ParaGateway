window.paraGateway = window.paraGateway || {};

(function (gateway) {
    function fallbackCopy(text) {
        const textarea = document.createElement('textarea');
        const activeElement = document.activeElement;
        textarea.value = text;
        textarea.readOnly = true;
        textarea.setAttribute('aria-hidden', 'true');
        textarea.style.cssText = 'position:fixed;left:0;top:0;width:1px;height:1px;opacity:0;pointer-events:none';
        document.body.appendChild(textarea);
        textarea.focus({ preventScroll: true });
        textarea.select();
        textarea.setSelectionRange(0, textarea.value.length);

        try {
            return document.execCommand('copy');
        } catch (_) {
            return false;
        } finally {
            textarea.remove();
            if (activeElement instanceof HTMLElement) {
                try { activeElement.focus({ preventScroll: true }); }
                catch (_) { activeElement.focus(); }
            }
        }
    }

    gateway.copyText = async function (value) {
        const text = String(value || '');
        if (!text) return false;

        if (window.isSecureContext && navigator.clipboard && typeof navigator.clipboard.writeText === 'function') {
            try {
                await navigator.clipboard.writeText(text);
                return true;
            } catch (_) { }
        }

        return fallbackCopy(text);
    };
})(window.paraGateway);

(function (gateway) {
    const themeKey = 'theme';
    const sidebarKey = 'paragateway.sidebar.collapsed';

    function readStorage(key) {
        try { return window.localStorage.getItem(key); }
        catch (_) { return null; }
    }

    function writeStorage(key, value) {
        try { window.localStorage.setItem(key, value); }
        catch (_) { }
    }

    function preferredTheme() {
        const stored = readStorage(themeKey);
        if (stored === 'dark' || stored === 'light') return stored;
        return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches
            ? 'dark'
            : 'light';
    }

    function applyTheme(value, persist) {
        const theme = value === 'dark' ? 'dark' : 'light';
        const root = document.documentElement;
        root.dataset.theme = theme;
        root.classList.toggle('dark', theme === 'dark');
        root.style.colorScheme = theme;

        const lightTheme = document.getElementById('dx-light-theme');
        const darkTheme = document.getElementById('dx-dark-theme');
        if (lightTheme) lightTheme.disabled = theme === 'dark';
        if (darkTheme) darkTheme.disabled = theme !== 'dark';
        if (persist) writeStorage(themeKey, theme);
        return theme;
    }

    gateway.getTheme = function () {
        return document.documentElement.dataset.theme || preferredTheme();
    };

    gateway.setTheme = function (theme) {
        return applyTheme(theme, true);
    };

    gateway.getSidebarCollapsed = function () {
        return readStorage(sidebarKey) === 'true';
    };

    gateway.setSidebarCollapsed = function (collapsed) {
        const value = Boolean(collapsed);
        writeStorage(sidebarKey, value ? 'true' : 'false');
        return value;
    };

    applyTheme(preferredTheme(), false);
})(window.paraGateway);

window.paraGateway.downloadBytes = function (base64, contentType, fileName) {
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let index = 0; index < binary.length; index++) {
        bytes[index] = binary.charCodeAt(index);
    }
    const blob = new Blob([bytes], { type: contentType || 'application/octet-stream' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName || 'download';
    anchor.rel = 'noopener';
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
};

window.paraGateway.downloadUrl = function (url) {
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.rel = 'noopener';
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
};

window.paraGateway.getUserAgent = function () {
    return navigator.userAgent || '';
};

window.paraGateway.isPasskeySupported = function () {
    return Boolean(window.PublicKeyCredential && navigator.credentials);
};

window.paraGateway.prompt = function (message, defaultValue) {
    return window.prompt(message, defaultValue || '') || '';
};

// Authentication challenge adapters. The Go API accepts the provider-neutral
// proof shape { turnstile_token, tencent_captcha_ticket, tencent_captcha_randstr }.
// SDKs are loaded only when the administrator enables the corresponding
// provider, keeping the default deployment independent of third-party scripts.
(function () {
    const captchaInstances = new Map();
    const scriptPromises = new Map();

    function loadScript(src, ready) {
        if (ready && ready()) return Promise.resolve();
        if (scriptPromises.has(src)) return scriptPromises.get(src);
        const promise = new Promise((resolve, reject) => {
            const existing = document.querySelector(`script[src="${src}"]`);
            if (existing) {
                existing.addEventListener('load', () => resolve(), { once: true });
                existing.addEventListener('error', () => reject(new Error('验证码 SDK 加载失败')), { once: true });
                return;
            }
            const script = document.createElement('script');
            script.src = src;
            script.async = true;
            script.defer = true;
            script.onload = () => resolve();
            script.onerror = () => reject(new Error('验证码 SDK 加载失败'));
            document.head.appendChild(script);
        });
        scriptPromises.set(src, promise);
        return promise;
    }

    function normalizeRegion(value) {
        return String(value || '').toLowerCase() === 'intl' ? 'intl' : 'cn';
    }

    function proof(turnstileToken, tencentTicket, tencentRandstr) {
        if (tencentTicket) {
            return { turnstile_token: null, tencent_captcha_ticket: tencentTicket, tencent_captcha_randstr: tencentRandstr || '' };
        }
        return { turnstile_token: turnstileToken || null, tencent_captcha_ticket: null, tencent_captcha_randstr: null };
    }

    async function loadTurnstile() {
        await loadScript('https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit', () => Boolean(window.turnstile));
        if (!window.turnstile) throw new Error('Turnstile SDK 不可用');
    }

    async function loadTencent(region) {
        const src = region === 'intl'
            ? 'https://ca.turing.captcha.qcloud.com/TJNCaptcha-global.js'
            : 'https://turing.captcha.qcloud.com/TJCaptcha.js';
        await loadScript(src, () => Boolean(window.TencentCaptcha));
        if (!window.TencentCaptcha) throw new Error('腾讯验证码 SDK 不可用');
    }

    async function loadAliyun() {
        await loadScript('https://o.alicdn.com/captcha-frontend/aliyunCaptcha/AliyunCaptcha.js', () => Boolean(window.initAliyunCaptcha));
        if (!window.initAliyunCaptcha) throw new Error('阿里云验证码 SDK 不可用');
    }

    function removeNode(id) {
        const node = document.getElementById(id);
        if (node) node.remove();
    }

    async function init(hostId, settings) {
        if (captchaInstances.has(hostId)) return;
        const host = document.getElementById(hostId);
        if (!host) return;
        const state = { settings, token: '', randstr: '', turnstileWidget: null, sdk: null, disposed: false };
        captchaInstances.set(hostId, state);
        state.settings.host_id = hostId;

        if (settings.turnstile_enabled && settings.turnstile_site_key) {
            try {
                await loadTurnstile();
                const container = document.getElementById(settings.turnstile_host_id) || host;
                state.turnstileWidget = window.turnstile.render(container, {
                    sitekey: settings.turnstile_site_key,
                    size: 'flexible',
                    callback: value => { state.token = value || ''; state.randstr = ''; },
                    'expired-callback': () => { state.token = ''; state.randstr = ''; },
                    'error-callback': () => { state.token = ''; state.randstr = ''; }
                });
            } catch (error) {
                captchaInstances.delete(hostId);
                throw error;
            }
        }
    }

    function createTencentVerification(state) {
        const settings = state.settings;
        const region = normalizeRegion(settings.tencent_region);
        const appId = settings.tencent_app_id;
        return new Promise(async (resolve, reject) => {
            try {
                await loadTencent(region);
                let container = null;
                let instance;
                const cleanup = () => {
                    if (instance && typeof instance.destroy === 'function') {
                        try { instance.destroy(); } catch (_) { }
                    }
                    if (container) container.remove();
                    if (state.sdk === instance) state.sdk = null;
                };
                const finish = result => {
                    const ticket = String(result && result.ticket || '').trim();
                    const rand = String(result && result.randstr || '').trim();
                    if (Number(result && result.ret) === 2 || !ticket) { cleanup(); resolve(null); return; }
                    if (ticket.startsWith('trerror_') || !rand) { cleanup(); reject(new Error('腾讯验证码校验失败')); return; }
                    state.token = ticket; state.randstr = rand;
                    cleanup();
                    resolve(proof('', ticket, rand));
                };
                if (region === 'intl') {
                    container = document.createElement('div');
                    container.className = 'paragateway-tencent-captcha-host';
                    document.body.appendChild(container);
                    instance = new window.TencentCaptcha(container, appId, finish, { enableAutoCheck: false, type: 'popup', userLanguage: 'zh-cn' });
                } else {
                    instance = new window.TencentCaptcha(appId, finish, { userLanguage: 'zh-cn' });
                }
                state.sdk = instance;
                instance.show();
            } catch (error) { reject(error); }
        });
    }

    async function createAliyunVerification(state) {
        await loadAliyun();
        const settings = state.settings;
        const buttonId = `${state.settings.host_id}-aliyun-button`;
        const elementId = `${state.settings.host_id}-aliyun-element`;
        const host = document.getElementById(state.settings.host_id);
        if (!host) return null;
        let button = document.getElementById(buttonId);
        if (!button) {
            button = document.createElement('button');
            button.id = buttonId;
            button.type = 'button';
            button.className = 'captcha-sdk-trigger';
            button.tabIndex = -1;
            button.setAttribute('aria-hidden', 'true');
            button.textContent = '完成安全验证';
            host.appendChild(button);
        }
        let element = document.getElementById(elementId);
        if (!element) { element = document.createElement('div'); element.id = elementId; host.appendChild(element); }
        return new Promise(resolve => {
            let settled = false;
            let popupSeen = false;
            const startedAt = Date.now();
            let watchTimer = null;
            const done = value => {
                if (settled) return;
                settled = true;
                if (watchTimer !== null) window.clearInterval(watchTimer);
                resolve(value);
            };
            window.AliyunCaptchaConfig = { region: settings.aliyun_region || 'cn', prefix: settings.aliyun_prefix };
            window.initAliyunCaptcha({
                SceneId: settings.aliyun_scene_id,
                prefix: settings.aliyun_prefix,
                mode: 'popup',
                element: `#${elementId}`,
                button: `#${buttonId}`,
                captchaVerifyCallback: param => {
                    const value = String(param || '').trim();
                    if (value) { state.token = value; state.randstr = ''; done(proof(value, '', '')); }
                    return { captchaResult: Boolean(value) };
                },
                onBizResultCallback: () => {},
                getInstance: () => {},
                language: 'cn'
            });
            const popupVisible = () => {
                const popup = document.getElementById('aliyunCaptcha-window-popup');
                return Boolean(popup && window.getComputedStyle(popup).display !== 'none');
            };
            watchTimer = window.setInterval(() => {
                if (popupVisible()) { popupSeen = true; return; }
                if (popupSeen || Date.now() - startedAt > 8000) { done(null); return; }
                button.click();
            }, 300);
            button.click();
        });
    }

    async function verify(hostId) {
        const state = captchaInstances.get(hostId);
        if (!state || state.disposed) return null;
        if (state.token) return proof(state.settings.tencent_enabled ? '' : state.token, state.settings.tencent_enabled ? state.token : '', state.randstr);
        if (state.settings.turnstile_enabled && state.settings.turnstile_site_key) return null;
        if (state.settings.tencent_enabled && state.settings.tencent_app_id) return createTencentVerification(state);
        if (state.settings.aliyun_enabled && state.settings.aliyun_scene_id && state.settings.aliyun_prefix) return createAliyunVerification(state);
        return null;
    }

    function reset(hostId) {
        const state = captchaInstances.get(hostId);
        if (!state) return;
        state.token = ''; state.randstr = '';
        if (state.sdk && typeof state.sdk.destroy === 'function') {
            try { state.sdk.destroy(); } catch (_) { }
            state.sdk = null;
        }
        removeNode('aliyunCaptcha-mask');
        removeNode('aliyunCaptcha-window-popup');
        removeNode(`${hostId}-aliyun-button`);
        removeNode(`${hostId}-aliyun-element`);
        if (state.turnstileWidget !== null && window.turnstile) {
            try { window.turnstile.reset(state.turnstileWidget); } catch (_) { }
        }
    }

    function dispose(hostId) {
        const state = captchaInstances.get(hostId);
        if (!state) return;
        state.disposed = true;
        if (state.sdk && typeof state.sdk.destroy === 'function') {
            try { state.sdk.destroy(); } catch (_) { }
        }
        if (state.turnstileWidget !== null && window.turnstile) {
            try { window.turnstile.remove(state.turnstileWidget); } catch (_) { }
        }
        removeNode(`${hostId}-aliyun-button`);
        removeNode(`${hostId}-aliyun-element`);
        document.querySelectorAll('.paragateway-tencent-captcha-host').forEach(node => node.remove());
        captchaInstances.delete(hostId);
    }

    window.paraGateway.captcha = { init, verify, reset, dispose };
}());

(function (gateway) {
    const escapeHandlers = new Map();
    let nextEscapeHandlerId = 1;

    gateway.registerEscapeHandler = function (dotNetReference) {
        const id = nextEscapeHandlerId++;
        const handler = function (event) {
            if (event.key !== 'Escape') return;
            dotNetReference.invokeMethodAsync('ExitFullscreenFromKeyboard').catch(function () { });
        };
        document.addEventListener('keydown', handler);
        escapeHandlers.set(id, handler);
        return id;
    };

    gateway.unregisterEscapeHandler = function (id) {
        const handler = escapeHandlers.get(id);
        if (!handler) return;
        document.removeEventListener('keydown', handler);
        escapeHandlers.delete(id);
    };
}(window.paraGateway = window.paraGateway || {}));

(function (gateway) {
    const avatarTargetBytes = 20 * 1024;
    const avatarScaleSteps = [1, 0.92, 0.84, 0.76, 0.68, 0.6, 0.52, 0.44, 0.36];
    const avatarQualitySteps = [0.92, 0.84, 0.76, 0.68, 0.6, 0.52, 0.44, 0.36];

    function readAsDataUrl(value) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = () => resolve(typeof reader.result === 'string' ? reader.result : '');
            reader.onerror = () => reject(reader.error || new Error('读取所选图片失败'));
            reader.readAsDataURL(value);
        });
    }

    function loadImage(value) {
        return new Promise((resolve, reject) => {
            const image = new Image();
            image.onload = () => resolve(image);
            image.onerror = () => reject(new Error('读取所选图片失败'));
            image.src = value;
        });
    }

    function canvasToBlob(canvas, quality) {
        return new Promise((resolve, reject) => {
            canvas.toBlob(blob => blob ? resolve(blob) : reject(new Error('压缩所选图片失败')), 'image/webp', quality);
        });
    }

    gateway.prepareAvatar = async function (input) {
        const file = input && input.files ? input.files[0] : null;
        if (input) input.value = '';
        if (!file) return '';
        if (!String(file.type || '').startsWith('image/')) throw new Error('请选择图片文件');
        if (file.type === 'image/gif') {
            if (file.size > avatarTargetBytes) throw new Error('GIF 头像必须在 20KB 以内');
            return await readAsDataUrl(file);
        }
        if (file.size <= avatarTargetBytes) return await readAsDataUrl(file);

        const source = await readAsDataUrl(file);
        const image = await loadImage(source);
        const canvas = document.createElement('canvas');
        const context = canvas.getContext('2d');
        if (!context) throw new Error('压缩所选图片失败');

        for (const scale of avatarScaleSteps) {
            const width = Math.max(1, Math.round(image.naturalWidth * scale));
            const height = Math.max(1, Math.round(image.naturalHeight * scale));
            canvas.width = width;
            canvas.height = height;
            context.clearRect(0, 0, width, height);
            context.drawImage(image, 0, 0, width, height);
            for (const quality of avatarQualitySteps) {
                const blob = await canvasToBlob(canvas, quality);
                if (blob.size <= avatarTargetBytes) return await readAsDataUrl(blob);
            }
        }
        throw new Error('无法将图片压缩到 20KB 以内，请换一张更小的图片');
    };
}(window.paraGateway));

(function () {
    function base64UrlToBuffer(value) {
        const normalized = String(value || '').replace(/-/g, '+').replace(/_/g, '/');
        const padded = normalized + '='.repeat((4 - normalized.length % 4) % 4);
        const binary = atob(padded);
        const bytes = Uint8Array.from(binary, ch => ch.charCodeAt(0));
        return bytes.buffer;
    }

    function bufferToBase64Url(value) {
        if (!value) return null;
        const bytes = new Uint8Array(value);
        let binary = '';
        for (const byte of bytes) binary += String.fromCharCode(byte);
        return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '');
    }

    function creationOptions(value) {
        const options = structuredClone(value || {});
        options.challenge = base64UrlToBuffer(options.challenge);
        if (options.user) options.user.id = base64UrlToBuffer(options.user.id);
        if (Array.isArray(options.excludeCredentials)) {
            options.excludeCredentials = options.excludeCredentials.map(item => ({
                ...item,
                id: base64UrlToBuffer(item.id)
            }));
        }
        return options;
    }

    function requestOptions(value) {
        const options = structuredClone(value || {});
        options.challenge = base64UrlToBuffer(options.challenge);
        if (Array.isArray(options.allowCredentials)) {
            options.allowCredentials = options.allowCredentials.map(item => ({
                ...item,
                id: base64UrlToBuffer(item.id)
            }));
        }
        return options;
    }

    function serialize(credential) {
        const response = credential.response;
        const base = {
            id: credential.id,
            rawId: bufferToBase64Url(credential.rawId),
            type: credential.type,
            authenticatorAttachment: credential.authenticatorAttachment,
            clientExtensionResults: credential.getClientExtensionResults(),
            response: {}
        };
        if (response.attestationObject) {
            base.response.attestationObject = bufferToBase64Url(response.attestationObject);
            base.response.clientDataJSON = bufferToBase64Url(response.clientDataJSON);
            base.response.transports = typeof response.getTransports === 'function' ? response.getTransports() : [];
        } else {
            base.response.authenticatorData = bufferToBase64Url(response.authenticatorData);
            base.response.clientDataJSON = bufferToBase64Url(response.clientDataJSON);
            base.response.signature = bufferToBase64Url(response.signature);
            base.response.userHandle = bufferToBase64Url(response.userHandle);
        }
        return base;
    }

    window.paraGateway.passkeyCreate = async function (publicKey) {
        if (!window.paraGateway.isPasskeySupported()) throw new Error('当前浏览器不支持 Passkey。');
        const credential = await navigator.credentials.create({ publicKey: creationOptions(publicKey) });
        if (!(credential instanceof PublicKeyCredential)) throw new Error('Passkey 注册已取消。');
        return serialize(credential);
    };

    window.paraGateway.passkeyGet = async function (publicKey) {
        if (!window.paraGateway.isPasskeySupported()) throw new Error('当前浏览器不支持 Passkey。');
        const credential = await navigator.credentials.get({ publicKey: requestOptions(publicKey) });
        if (!(credential instanceof PublicKeyCredential)) throw new Error('Passkey 登录已取消。');
        return serialize(credential);
    };
}());
