package com.kagultd.erp.api

import com.kagultd.erp.generated.api.apis.SalesOrdersApi
import okhttp3.Call
import okhttp3.HttpUrl

class KaguApiClientFactory(
    baseUrl: HttpUrl,
    private val callFactory: Call.Factory,
    private val accessTokenProvider: () -> String?,
) {
    private val normalizedBaseUrl = baseUrl.toString().removeSuffix("/")

    init {
        require(baseUrl.isHttps || baseUrl.host in DEVELOPMENT_LOOPBACK_HOSTS) {
            "Kagu API base URL must use HTTPS outside the local development loopback."
        }
    }

    fun salesOrders(): SalesOrdersApi =
        SalesOrdersApi(normalizedBaseUrl, callFactory).also { api ->
            api.accessTokenProvider = accessTokenProvider
        }

    private companion object {
        val DEVELOPMENT_LOOPBACK_HOSTS = setOf("10.0.2.2", "127.0.0.1", "localhost")
    }
}
