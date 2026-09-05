import { Configuration, SalesOrdersApi } from "@kaguerp/api-client";

const webApiConfiguration = new Configuration({
  basePath: "",
  credentials: "same-origin",
});

/**
 * Generated transport configured for the web BFF boundary.
 * Browser code never receives or persists an OIDC bearer token.
 */
export const salesOrdersApi = new SalesOrdersApi(webApiConfiguration);
