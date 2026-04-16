import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { App } from "./routes/App";
import { PermissionProvider } from "./security/permissions";
import { ThemeProvider } from "./design/theme/ThemeProvider";
import { I18nProvider } from "./i18n/I18nProvider";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <BrowserRouter>
      <I18nProvider>
        <ThemeProvider>
          <PermissionProvider>
            <App />
          </PermissionProvider>
        </ThemeProvider>
      </I18nProvider>
    </BrowserRouter>
  </React.StrictMode>
);

