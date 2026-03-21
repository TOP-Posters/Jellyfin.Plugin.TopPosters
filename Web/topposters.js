const TopPostersConfig = {
    pluginUniqueId: 'a3f2e1d4-5b6c-7d8e-9f0a-1b2c3d4e5f6a'
};

export default function (view) {
    view.addEventListener('viewshow', function () {
        Dashboard.showLoadingMsg();
        const page = this;
        ApiClient.getPluginConfiguration(TopPostersConfig.pluginUniqueId).then(function (config) {
            page.querySelector('#apiKey').value = config.ApiKey || '';
            Dashboard.hideLoadingMsg();
        });
    });

    view.querySelector('#TopPostersConfigForm').addEventListener('submit', function (e) {
        e.preventDefault();
        Dashboard.showLoadingMsg();
        const form = this;
        ApiClient.getPluginConfiguration(TopPostersConfig.pluginUniqueId).then(function (config) {
            config.ApiKey = form.querySelector('#apiKey').value.trim();
            ApiClient.updatePluginConfiguration(TopPostersConfig.pluginUniqueId, config).then(function (result) {
                Dashboard.processPluginConfigurationUpdateResult(result);
            });
        });
        return false;
    });
}
