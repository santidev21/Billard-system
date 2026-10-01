// Karma configuration file for Angular testing
module.exports = function (config) {
  config.set({
    basePath: '',
    frameworks: ['jasmine', '@angular-devkit/build-angular'],
    plugins: [
      require('karma-jasmine'),
      require('karma-chrome-launcher'),
      require('karma-jasmine-html-reporter'),
      require('karma-coverage'),
      require('@angular-devkit/build-angular/plugins/karma'),
    ],
    client: {
      jasmine: {},
      clearContext: false,
    },
    jasmineHtmlReporter: {
      suppressAll: true,
    },
    coverageReporter: {
      dir: require('path').join(__dirname, './coverage/billiard-frontend'),
      subdir: '.',
      reporters: [{ type: 'html' }, { type: 'text-summary' }],
      // Thresholds raised with the service specs (auth + API contract tests):
      // measured coverage is ~96/65/98/96, so these leave margin while still
      // failing if the frontend logic regresses.
      check: {
        global: {
          statements: 90,
          branches: 60,
          functions: 90,
          lines: 90,
        },
      },
    },
    reporters: ['progress', 'kjhtml', 'coverage'],
    browsers: ['ChromeHeadless'],
    customLaunchers: {
      ChromeHeadlessNoSandbox: {
        base: 'ChromeHeadless',
        flags: [
          '--no-sandbox',
          '--disable-gpu',
          '--disable-dev-shm-usage',
          '--disable-setuid-sandbox',
        ],
      },
    },
    restartOnFileChange: true,
  });
};
