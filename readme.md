# miniserve

Simple static webserver with automation tasks for JS, LESS, and HTML compiling

Example config file:

```json
{
  "port": 80,
  "path": "build",
  "automations": [
    {
      "tagName": "css",
      "type": "less",
      "minify": true,
      "sourceFiles": [
        "source/less/*.less"
      ]
    },
    {
      "tagName": "js",
      "type": "js",
      "minify": true,
      "sourceFiles": [
        "source/js/libs/*.js",
        "source/js/*.js"
      ]
    },
    {
      "tagName": "html",
      "type": "html",
      "minify": true,
      "sourceFiles": [
        "source/html/index.html"
      ],
      "destFile": "build/index.html",
      "parseTags": true
    }
  ]
}
```

The ```parseTags``` setting allows the program to search for tags and replace then with pre-compiled content. If you put a tag ```{{ my-js-file }}``` inside your HTML file it will, upon compilation, be replaced with the parsed JS content. The tag name it looks for is configured with the ```tagName``` param.

The webserver part is fetched from: <https://gist.github.com/aksakalli/9191056>