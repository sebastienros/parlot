#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use std::io::Read;
use tauri::{Manager, WebviewUrl, WebviewWindowBuilder};

fn session_url(value: &str) -> Result<tauri::Url, String> {
    let url = tauri::Url::parse(value).map_err(|_| "Invalid session URL")?;
    if url.scheme() != "http"
        || url.host_str() != Some("127.0.0.1")
        || url.port().is_none()
        || !url.username().is_empty()
        || url.password().is_some()
        || url.path() != "/"
        || url.query().is_some()
        || !url
            .fragment()
            .is_some_and(|token| token.len() == 64 && token.bytes().all(|c| c.is_ascii_hexdigit()))
    {
        return Err("Expected a local Parlot Explorer session URL".into());
    }
    Ok(url)
}

fn main() {
    let url = std::env::args()
        .nth(1)
        .ok_or("Missing session URL")
        .and_then(|value| session_url(&value).map_err(|_| "Invalid session URL"));
    let url = match url {
        Ok(url) => url,
        Err(message) => {
            eprintln!("{message}");
            std::process::exit(1);
        }
    };
    let origin = url.origin();
    let result = tauri::Builder::default()
        .setup(move |app| {
            WebviewWindowBuilder::new(app, "main", WebviewUrl::External(url))
                .title("Parlot Explorer")
                .inner_size(1440.0, 960.0)
                .min_inner_size(850.0, 650.0)
                .on_navigation(move |destination| destination.origin() == origin)
                .on_new_window(|_, _| tauri::webview::NewWindowResponse::Deny)
                .build()?;
            // The .NET parent holds stdin open. EOF also closes the app if the parent crashes.
            let handle = app.handle().clone();
            std::thread::spawn(move || {
                let mut byte = [0];
                while std::io::stdin()
                    .read(&mut byte)
                    .is_ok_and(|count| count != 0)
                {}
                handle.exit(0);
            });
            Ok(())
        })
        .on_window_event(|window, event| {
            if matches!(event, tauri::WindowEvent::Destroyed) {
                window.app_handle().exit(0);
            }
        })
        .run(tauri::generate_context!());
    if let Err(error) = result {
        eprintln!("Could not open the desktop window: {error}");
        std::process::exit(1);
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn accepts_only_a_local_session() {
        let token = "a".repeat(64);
        assert!(session_url(&format!("http://127.0.0.1:12345/#{token}")).is_ok());
        for url in [
            "https://example.com/",
            "http://localhost:12345/",
            "file:///tmp/index.html",
            "http://127.0.0.1:12345/#bad",
        ] {
            assert!(session_url(url).is_err());
        }
        assert!(session_url(&format!("http://user@127.0.0.1:12345/#{token}")).is_err());
        assert!(session_url(&format!("http://127.0.0.1:12345/other#{token}")).is_err());
    }
}
