 /*
  * The library is not extensively tested and only
  * meant as a simple explanation and for inspiration.
  * NO WARRANTY of ANY KIND is provided.
  */


#if defined(MPU_USES_TWI) // Use TWI drivers

#include <stdbool.h>
#include <stdint.h>
#include <string.h>
#include "nrf_drv_twi.h"
#include "nrf_drv_mpu.h"
#include "app_util_platform.h"
#include "nrf_gpio.h"
#include "nrf_log.h"


/* Pins to connect MPU. Pinout is different for nRF51 DK and nRF52 DK
 * and therefore I have added a conditional statement defining different pins
 * for each board. This is only for my own convenience. 
 */
#define MPU_TWI_SCL_PIN 27
#define MPU_TWI_SDA_PIN 26


#define MPU_TWI_BUFFER_SIZE     	16 // 14 byte buffers will suffice to read acceleromter, gyroscope and temperature data in one transmission.
#define MPU_TWI_TIMEOUT 			1000000 //default value 10000
#define MPU_ADDRESS     			0x68 
#define MPU_AK89XX_MAGN_ADDRESS     0x0C


static const nrf_drv_twi_t m_twi_instance = NRF_DRV_TWI_INSTANCE(0);
volatile static bool twi_tx_done = false;
volatile static bool twi_rx_done = false;

uint8_t twi_tx_buffer[MPU_TWI_BUFFER_SIZE];


static void nrf_drv_mpu_twi_event_handler(nrf_drv_twi_evt_t const * p_event, void * p_context)
{
    switch(p_event->type)
    {
        case NRF_DRV_TWI_EVT_DONE:
            switch(p_event->xfer_desc.type)
            {
                case NRF_DRV_TWI_XFER_TX:
                    twi_tx_done = true;
                    break;
                case NRF_DRV_TWI_XFER_TXTX:
                    twi_tx_done = true;
                    break;
                case NRF_DRV_TWI_XFER_RX:
                    twi_rx_done = true;
                    break;
                case NRF_DRV_TWI_XFER_TXRX:
                    twi_rx_done = true;
                    break;
                default:
                    break;
            }
            break;
        case NRF_DRV_TWI_EVT_ADDRESS_NACK:
            break;
        case NRF_DRV_TWI_EVT_DATA_NACK:
            break;
        default:
            break;
    }
}



/**
 * @brief TWI initialization.
 * Just the usual way. Nothing special here
 */
uint32_t nrf_drv_mpu_init(void)
{
    uint32_t err_code;
    
    const nrf_drv_twi_config_t twi_mpu_config = {
       .scl                = MPU_TWI_SCL_PIN,
       .sda                = MPU_TWI_SDA_PIN,
       .frequency          = NRF_TWI_FREQ_400K,
       .interrupt_priority = APP_IRQ_PRIORITY_HIGHEST,
       .clear_bus_init     = false
    };
    
    err_code = nrf_drv_twi_init(&m_twi_instance, &twi_mpu_config, nrf_drv_mpu_twi_event_handler, NULL);
    if(err_code != NRF_SUCCESS)
	{
		return err_code;
	}
    
    nrf_drv_twi_enable(&m_twi_instance);
	
	return NRF_SUCCESS;
}




// The TWI driver is not able to do two transmits without repeating the ADDRESS + Write bit byte
// Hence we need to merge the MPU register address with the buffer and then transmit all as one transmission
static void merge_register_and_data(uint8_t * new_buffer, uint8_t reg, uint8_t * p_data, uint32_t length)
{
    new_buffer[0] = reg;
    memcpy((new_buffer + 1), p_data, length);
}


uint32_t nrf_drv_mpu_write_registers(uint8_t reg, uint8_t * p_data, uint32_t length)
{
    // This burst write function is not optimal and needs improvement.
    // The new SDK 11 TWI driver is not able to do two transmits without repeating the ADDRESS + Write bit byte
    uint32_t err_code;
    uint32_t timeout = MPU_TWI_TIMEOUT;

    // Merging MPU register address and p_data into one buffer.
    merge_register_and_data(twi_tx_buffer, reg, p_data, length);

    // Setting up transfer
    nrf_drv_twi_xfer_desc_t xfer_desc;
    xfer_desc.address = MPU_ADDRESS;
    xfer_desc.type = NRF_DRV_TWI_XFER_TX;
    xfer_desc.primary_length = length + 1;
    xfer_desc.p_primary_buf = twi_tx_buffer;

    // Transferring
    err_code = nrf_drv_twi_xfer(&m_twi_instance, &xfer_desc, 0);

    while((!twi_tx_done) && --timeout);
    if(!timeout) return NRF_ERROR_TIMEOUT;
    twi_tx_done = false;

    return err_code;
}

uint32_t nrf_drv_mpu_write_single_register(uint8_t reg, uint8_t data)
{
    uint32_t err_code;
    uint32_t timeout = MPU_TWI_TIMEOUT;

    uint8_t packet[2] = {reg, data};

    err_code = nrf_drv_twi_tx(&m_twi_instance, MPU_ADDRESS, packet, 2, false);
    if(err_code != NRF_SUCCESS) return err_code;

    while((!twi_tx_done) && --timeout);
    if(!timeout) return NRF_ERROR_TIMEOUT;

    twi_tx_done = false;

    return err_code;
}


uint32_t nrf_drv_mpu_read_registers(uint8_t reg, uint8_t * p_data, uint32_t length)
{
    uint32_t err_code;
    uint32_t timeout = MPU_TWI_TIMEOUT;

    err_code = nrf_drv_twi_tx(&m_twi_instance, MPU_ADDRESS, &reg, 1, false);
    if(err_code != NRF_SUCCESS) return err_code;

    while((!twi_tx_done) && --timeout);
    if(!timeout) return NRF_ERROR_TIMEOUT;
    twi_tx_done = false;

    err_code = nrf_drv_twi_rx(&m_twi_instance, MPU_ADDRESS, p_data, length);
    if(err_code != NRF_SUCCESS) return err_code;

    timeout = MPU_TWI_TIMEOUT;
    while((!twi_rx_done) && --timeout);
    if(!timeout) return NRF_ERROR_TIMEOUT;
    twi_rx_done = false;

    return err_code;
}


#if (defined(MPU9150) || defined(MPU9255)) && (TWI_COUNT >= 1) // Magnetometer only works with TWI so check if TWI is enabled


uint32_t nrf_drv_mpu_read_magnetometer_registers(uint8_t reg, uint8_t * p_data, uint32_t length)
{
    uint32_t err_code;
    uint32_t timeout = MPU_TWI_TIMEOUT;

    err_code = nrf_drv_twi_tx(&m_twi_instance, MPU_AK89XX_MAGN_ADDRESS, &reg, 1, false);
    if(err_code != NRF_SUCCESS) return err_code;

    while((!twi_tx_done) && --timeout);
    if(!timeout) return NRF_ERROR_TIMEOUT;
    twi_tx_done = false;

    err_code = nrf_drv_twi_rx(&m_twi_instance, MPU_AK89XX_MAGN_ADDRESS, p_data, length);
    if(err_code != NRF_SUCCESS) return err_code;

    timeout = MPU_TWI_TIMEOUT;
    while((!twi_rx_done) && --timeout);
    if(!timeout) return NRF_ERROR_TIMEOUT;
    twi_rx_done = false;

    return err_code;
}


uint32_t nrf_drv_mpu_write_magnetometer_register(uint8_t reg, uint8_t data)
{
    uint32_t err_code;
    uint32_t timeout = MPU_TWI_TIMEOUT;

    uint8_t packet[2] = {reg, data};

    err_code = nrf_drv_twi_tx(&m_twi_instance, MPU_AK89XX_MAGN_ADDRESS, packet, 2, false);
    if(err_code != NRF_SUCCESS) return err_code;

    while((!twi_tx_done) && --timeout);
    if(!timeout) return NRF_ERROR_TIMEOUT;

    twi_tx_done = false;

    return err_code;
}

uint32_t mpu_twi_read_test(uint8_t slave_addr,
											uint8_t reg_addr,
											uint32_t length,
											uint8_t *data)
{
	  uint32_t err_code;
		uint32_t timeout = MPU_TWI_TIMEOUT;
	
	  err_code = nrf_drv_twi_tx(&m_twi_instance, slave_addr, &reg_addr, 1, false);
		if(err_code != NRF_SUCCESS) {
				NRF_LOG_RAW_INFO("error return from twi tx \r\n");
				return err_code;
		}
    while((!twi_tx_done) && --timeout);
    if(!timeout) return NRF_ERROR_TIMEOUT;
    twi_tx_done = false;
	
	  err_code = nrf_drv_twi_rx(&m_twi_instance, slave_addr, data, length);
    if(err_code != NRF_SUCCESS){
				NRF_LOG_RAW_INFO("error return from twi rx \n");
				return err_code;
		}		
		timeout = MPU_TWI_TIMEOUT;
    while((!twi_rx_done) && --timeout);
    if(!timeout) return NRF_ERROR_TIMEOUT;
    twi_rx_done = false;
	
		return err_code;
}

uint32_t mpu_twi_write_single_test(uint8_t slave_addr,
													uint8_t reg_addr,
													uint32_t length,
													uint8_t *data)
{
		uint32_t err_code;
		uint32_t timeout = MPU_TWI_TIMEOUT;
	
    uint8_t packet[2] = {reg_addr, *data};

    err_code = nrf_drv_twi_tx(&m_twi_instance, slave_addr, packet, 2, false);
    if(err_code != NRF_SUCCESS) return err_code;

    while((!twi_tx_done) && --timeout);
    if(!timeout) return NRF_ERROR_TIMEOUT;

    twi_tx_done = false;
		
		return err_code;
}

uint32_t i2c_read_porting(unsigned char slave_addr,
													unsigned char reg_addr,
													unsigned char length,
													unsigned char *data)
{
		uint32_t err_code;
		uint8_t *pdata;
		pdata=(uint8_t*)data;
		err_code=mpu_twi_read_test((uint8_t)slave_addr, (uint8_t)reg_addr, (uint32_t)length, pdata);
		return err_code;
}

uint32_t i2c_write_porting(unsigned char slave_addr,
													unsigned char reg_addr,
													unsigned char length,
													unsigned char *data)
{
		uint32_t err_code;
		uint32_t timeout = MPU_TWI_TIMEOUT;
		uint8_t packet[(int)length+1];
		packet[0]=(uint8_t)reg_addr;
		memcpy(&packet[1], data, length);
	
    err_code = nrf_drv_twi_tx(&m_twi_instance, slave_addr, packet, length+1, false);
    if(err_code != NRF_SUCCESS){
			printf("twi_tx error");
			return err_code;
		}
    while((!twi_tx_done) && --timeout);
    if(!timeout) return NRF_ERROR_TIMEOUT;

    twi_tx_done = false;
		
		return err_code;
}														

uint32_t mpu_twi_write_test(uint8_t slave_addr,
													uint8_t reg_addr,
													uint32_t length,
													unsigned char *data)
{
		uint32_t err_code;
		uint32_t timeout = MPU_TWI_TIMEOUT;
		
		for(uint32_t i=0;i<length;i++){
		
			uint8_t packet[2]={reg_addr+i,*(data+i)};
			err_code = nrf_drv_twi_tx(&m_twi_instance, slave_addr, packet, 2, false);
			if(err_code != NRF_SUCCESS) return err_code;

			while((!twi_tx_done) && --timeout);
			if(!timeout) return NRF_ERROR_TIMEOUT;

			twi_tx_done = false;
		}
		
		return err_code;

}
#endif // (defined(MPU9150) || defined(MPU9255)) && (TWI_COUNT >= 1) // Magnetometer only works with TWI so check if TWI is enabled


#endif // Use TWI drivers

/**
  @}
*/
