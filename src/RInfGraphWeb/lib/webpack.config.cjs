const webpack = require('webpack');

var babelOptions = {
    presets: [
        ["@babel/preset-env", {
            "targets": {
                "node": true,
            },
        }]
    ],
    plugins: ['@babel/plugin-syntax-import-attributes']
};

console.log("Bundling function...");

module.exports = {
    performance: {
        hints: false,
    },
    experiments: {
        outputModule: true
    },
    target: "web",
    mode: "production",
    entry: './index.js',
    output: {
        filename: 'bundle.js',
        library: {
            type: 'module'
        }
    },
    plugins: [new webpack.ProvidePlugin({
        Buffer: ['buffer', 'Buffer'],
    })],
    module: {
        rules: [
            {
                test: /\.js$/,
                exclude: /node_modules/,
                use: {
                    loader: 'babel-loader',
                    options: babelOptions
                },
            }
        ]
    },
};