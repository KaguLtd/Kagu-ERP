package com.kagultd.erp

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import com.kagultd.erp.ui.KaguErpApp
import com.kagultd.erp.ui.theme.KaguErpTheme

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            KaguErpTheme {
                KaguErpApp()
            }
        }
    }
}

