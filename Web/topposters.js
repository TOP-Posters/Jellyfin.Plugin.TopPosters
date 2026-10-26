const pluginUniqueId = 'a3f2e1d4-5b6c-7d8e-9f0a-1b2c3d4e5f6a';
const refreshTaskKey = 'TopPostersRefreshArtwork';
const switches = {
    EnablePosters: ['enablePosters', true],
    EnableSeasonPosters: ['enableSeasonPosters', true],
    EnableEpisodeThumbnails: ['enableEpisodeThumbnails', true],
    EnableLogos: ['enableLogos', false],
    EnableBackdrops: ['enableBackdrops', false],
    EnableArtworkRefresh: ['enableArtworkRefresh', false]
};

export default function (view) {
    const form = view.querySelector('#TopPostersConfigForm');
    const refresh = view.querySelector('#refreshArtwork');
    const status = view.querySelector('#refreshStatus');
    let busy = false;

    function setBusy(value) {
        busy = value;
        form.querySelector('[type="submit"]').disabled = value;
        refresh.disabled = value;
        if (value) Dashboard.showLoadingMsg();
        else Dashboard.hideLoadingMsg();
    }

    view.addEventListener('viewshow', async function () {
        setBusy(true);
        status.textContent = '';
        try {
            const config = await ApiClient.getPluginConfiguration(pluginUniqueId);
            view.querySelector('#apiKey').value = config.ApiKey || '';
            for (const [key, [id, fallback]] of Object.entries(switches)) {
                view.querySelector('#' + id).checked = config[key] ?? fallback;
            }
            view.querySelector('#refreshBatchSize').value = config.RefreshBatchSize ?? 100;
        } catch {
            Dashboard.alert('Could not load TOP Posters settings. Please try again.');
        } finally {
            setBusy(false);
        }
    });

    async function save() {
        const config = await ApiClient.getPluginConfiguration(pluginUniqueId);
        config.ApiKey = view.querySelector('#apiKey').value.trim();
        for (const [key, [id]] of Object.entries(switches)) config[key] = view.querySelector('#' + id).checked;
        config.RefreshBatchSize = Math.max(1, Math.min(1000, Number(view.querySelector('#refreshBatchSize').value) || 100));
        return ApiClient.updatePluginConfiguration(pluginUniqueId, config);
    }

    form.addEventListener('submit', async function (event) {
        event.preventDefault();
        if (busy || !form.reportValidity()) return;
        setBusy(true);
        try {
            const result = await save();
            Dashboard.processPluginConfigurationUpdateResult(result);
        } catch {
            Dashboard.alert('Could not save TOP Posters settings. Please try again.');
        } finally {
            setBusy(false);
        }
    });

    refresh.addEventListener('click', async function () {
        if (busy || !form.reportValidity()) return;
        if (!view.querySelector('#enableArtworkRefresh').checked) {
            status.textContent = 'Turn on Enable artwork refresh before starting the task.';
            return;
        }
        if (!view.querySelector('#apiKey').value.trim()) {
            status.textContent = 'Enter your TOP Posters API key first.';
            return;
        }
        setBusy(true);
        status.textContent = '';
        try {
            await save();
            const tasks = await ApiClient.getScheduledTasks();
            const task = tasks.find(entry => entry.Key === refreshTaskKey);
            if (!task) {
                status.textContent = 'Settings saved. Restart Jellyfin to register the artwork refresh task.';
                return;
            }
            if (task.State === 'Running' || task.State === 'Cancelling') {
                status.textContent = 'The artwork refresh task is already active. View its progress in Scheduled Tasks.';
                return;
            }
            await ApiClient.startScheduledTask(task.Id);
            status.textContent = 'Artwork refresh started. View progress and results in Scheduled Tasks → TOP Posters.';
        } catch {
            status.textContent = 'Could not start artwork refresh. Check the saved settings and Scheduled Tasks, then try again.';
        } finally {
            setBusy(false);
        }
    });
}
