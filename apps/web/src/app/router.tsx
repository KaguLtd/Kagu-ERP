import { createBrowserRouter } from "react-router-dom";

import { HomePage } from "../routes/HomePage";

export const router = createBrowserRouter([
  {
    path: "/",
    element: <HomePage />,
  },
  {
    path: "/reports/party-account",
    lazy: async () => {
      const { PartyAccountReportPage } = await import("../features/party-report");
      return { Component: PartyAccountReportPage };
    },
  },
]);
