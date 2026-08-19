package com.kagultd.erp.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import com.kagultd.erp.model.WorkingContext
import com.kagultd.erp.ui.theme.KaguErpTheme

@Composable
fun KaguErpApp(
    workingContext: WorkingContext = WorkingContext.Empty,
) {
    Scaffold { innerPadding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding),
        ) {
            Surface(
                modifier = Modifier.fillMaxWidth(),
                color = MaterialTheme.colorScheme.surface,
                tonalElevation = 1.dp,
            ) {
                Column(
                    modifier = Modifier.padding(horizontal = 20.dp, vertical = 16.dp),
                    verticalArrangement = Arrangement.spacedBy(12.dp),
                ) {
                    Text(
                        text = "Kagu ERP",
                        style = MaterialTheme.typography.titleLarge,
                        fontWeight = FontWeight.SemiBold,
                    )
                    ContextRow(label = "Şirket", value = workingContext.companyName ?: "Henüz seçilmedi")
                    ContextRow(label = "Şube", value = workingContext.branchName ?: "—")
                    ContextRow(label = "Dönem", value = workingContext.periodName ?: "—")
                    ContextRow(label = "Para birimi", value = workingContext.currencyCode ?: "—")
                }
            }

            HorizontalDivider()

            Column(
                modifier = Modifier.padding(20.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                Text(
                    text = "Güvenli mobil çalışma alanı",
                    style = MaterialTheme.typography.headlineSmall,
                )
                Text(
                    text = "İlk mobil kapsam; sorgu, onay ve kontrollü saha görevlerine ayrılmıştır.",
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    style = MaterialTheme.typography.bodyLarge,
                )
            }
        }
    }
}

@Composable
private fun ContextRow(label: String, value: String) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        Text(text = label, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Text(text = value, fontWeight = FontWeight.Medium)
    }
}

@Preview(showBackground = true)
@Composable
private fun KaguErpAppPreview() {
    KaguErpTheme {
        KaguErpApp()
    }
}

