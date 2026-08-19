package com.kagultd.erp.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

private val KaguColorScheme = lightColorScheme(
    primary = Color(0xFF264A8A),
    onPrimary = Color.White,
    background = Color(0xFFF7F8FA),
    surface = Color.White,
    onSurface = Color(0xFF172033),
    onSurfaceVariant = Color(0xFF5D6779),
    error = Color(0xFFB42318),
)

@Composable
fun KaguErpTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = KaguColorScheme,
        content = content,
    )
}

