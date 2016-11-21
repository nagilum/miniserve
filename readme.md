# miniserve

Simple static webserver with automation tasks for JS, LESS, and HTML compiling

Example config file:

```json
{
	"port": 80,
	"folder": "s3",
	"automations": [
		{
			"tagName": "my-css-file",
			"type": "less",
			"minify": true,
			"sourceFiles": [
				"source/less/*.less"
			],
			"destFile": "s3/app.css",
			"waitBeforeParsing": 100
		},
		{
			"tagName": "my-js-file",
			"type": "js",
			"minify": true,
			"sourceFiles": [
				"source/js/*.js"
			],
			"destFile": "s3/app.js",
			"waitBeforeParsing": 100
		},
		{
			"tagName": "my-html-file",
			"type": "html",
			"minify": true,
			"sourceFiles": [
				"source/html/app.html"
			],
			"destFile": "s3/index.html",
			"parseTags": true
		}
	]
}
```

The ```parseTags``` setting allows the program to search for tags and replace then with pre-compiled content. If you put a tag ```{{ my-js-file }}``` inside your HTML file it will, upon compilation, be replaced with the parsed JS content. The tag name it looks for is configured with the ```tagName``` param.

The webserver part is fetched from: <https://gist.github.com/aksakalli/9191056>