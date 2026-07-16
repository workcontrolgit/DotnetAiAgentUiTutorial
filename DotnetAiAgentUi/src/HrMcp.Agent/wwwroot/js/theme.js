window.theme = {
    get: function () {
        return localStorage.getItem('hr-theme') ?? 'light';
    },
    set: function (name) {
        localStorage.setItem('hr-theme', name);
        document.documentElement.setAttribute('data-theme', name);
    }
};
